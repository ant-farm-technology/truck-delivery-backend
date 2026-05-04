using System.Text.Json;
using MediatR;
using TruckDelivery.Driver.Application.IntegrationEvents;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Driver.Application.Commands.AssignVehicleToDriver;

public sealed class AssignVehicleToDriverCommandHandler(
    IDriverRepository driverRepository,
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork,
    IOutboxRepository outboxRepository)
    : IRequestHandler<AssignVehicleToDriverCommand, Result>
{
    public async Task<Result> Handle(AssignVehicleToDriverCommand request, CancellationToken ct)
    {
        var driver = await driverRepository.GetByIdAsync(request.DriverId, ct);
        if (driver is null)
            return Result.Failure(Error.NotFound("Driver", request.DriverId));

        var vehicle = await vehicleRepository.GetByIdAsync(request.VehicleId, ct);
        if (vehicle is null)
            return Result.Failure(Error.NotFound("Vehicle", request.VehicleId));

        if (driver.CurrentVehicleId.HasValue)
            return Result.Failure(Error.Conflict("Driver", "Driver already has a vehicle assigned."));

        var assignVehicleResult = vehicle.AssignDriver(request.DriverId);
        if (assignVehicleResult.IsFailure)
            return assignVehicleResult;

        var assignDriverResult = driver.AssignVehicle(request.VehicleId);
        if (assignDriverResult.IsFailure)
            return assignDriverResult;

        driverRepository.Update(driver);
        vehicleRepository.Update(vehicle);

        var @event = new VehicleAssignedToDriverEvent(
            vehicle.Id, driver.Id, vehicle.Type.ToString(), vehicle.MaxWeightKg);

        await outboxRepository.AddAsync(OutboxMessage.Create(
            eventType: nameof(VehicleAssignedToDriverEvent),
            topic: "driver.vehicle.assigned",
            partitionKey: driver.Id.ToString(),
            payload: JsonSerializer.Serialize(@event)), ct);

        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
