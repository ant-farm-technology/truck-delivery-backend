using System.Text.Json;
using MediatR;
using TruckDelivery.Driver.Application.Interfaces;
using TruckDelivery.Driver.Application.IntegrationEvents;
using TruckDelivery.Driver.Domain.Aggregates;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Driver.Application.Commands.ReportBreakdown;

public sealed class ReportBreakdownCommandHandler(
    IDriverRepository driverRepository,
    IVehicleRepository vehicleRepository,
    IBreakdownReportRepository breakdownReportRepository,
    IBreakdownFraudGate fraudGate,
    IOutboxRepository outboxRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ReportBreakdownCommand, Result<ReportBreakdownResult>>
{
    public async Task<Result<ReportBreakdownResult>> Handle(ReportBreakdownCommand request, CancellationToken ct)
    {
        var driver = await driverRepository.GetByIdAsync(request.DriverId, ct);
        if (driver is null)
            return Result.Failure<ReportBreakdownResult>(Error.NotFound("Driver", $"Driver {request.DriverId} not found."));

        var gateResult = await fraudGate.ValidateAsync(
            request.DriverId,
            request.Latitude,
            request.Longitude,
            request.PhotoUrls,
            driver.TrustScore,
            ct);

        if (!gateResult.Passed)
        {
            return Result.Failure<ReportBreakdownResult>(
                Error.Conflict("Breakdown.FraudGate", gateResult.FailureReason ?? "Breakdown report rejected by fraud gate."));
        }

        driver.ReportBreakdown(request.Latitude, request.Longitude, gateResult.RiskLevel);

        if (driver.CurrentVehicleId.HasValue)
        {
            var vehicle = await vehicleRepository.GetByIdAsync(driver.CurrentVehicleId.Value, ct);
            if (vehicle is not null)
            {
                vehicle.MarkBreakdown();
                vehicleRepository.Update(vehicle);
            }
        }

        driverRepository.Update(driver);

        var report = BreakdownReport.Create(
            request.DriverId,
            driver.CurrentVehicleId,
            request.Latitude,
            request.Longitude,
            request.PhotoUrls,
            gateResult.RiskLevel);

        await breakdownReportRepository.AddAsync(report, ct);

        var @event = new VehicleBreakdownEvent
        {
            DriverId = request.DriverId,
            VehicleId = driver.CurrentVehicleId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            PhotoUrls = request.PhotoUrls,
            TrustScore = driver.TrustScore,
            FraudRiskLevel = gateResult.RiskLevel
        };

        await outboxRepository.AddAsync(OutboxMessage.Create(
            nameof(VehicleBreakdownEvent),
            "driver.vehicle.breakdown",
            request.DriverId.ToString(),
            JsonSerializer.Serialize(@event)), ct);

        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new ReportBreakdownResult(report.Id, gateResult.RiskLevel, true));
    }
}
