using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TruckDelivery.Shipment.Domain.Aggregates;
using TruckDelivery.Shipment.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Shipment.Application.Commands.HandleBreakdown;

public sealed class HandleVehicleBreakdownCommandHandler(
    IShipmentRepository shipmentRepository,
    IUnitOfWork unitOfWork,
    ILogger<HandleVehicleBreakdownCommandHandler> logger)
    : IRequestHandler<HandleVehicleBreakdownCommand, Result>
{
    public async Task<Result> Handle(HandleVehicleBreakdownCommand request, CancellationToken ct)
    {
        var shipment = await shipmentRepository.GetActiveByDriverIdAsync(request.DriverId, ct);

        if (shipment is null)
        {
            logger.LogInformation(
                "No active InProgress shipment found for broken-down Driver={DriverId} — nothing to reassign",
                request.DriverId);
            return Result.Success();
        }

        var reason = $"Driver {request.DriverId} reported vehicle breakdown (risk: {request.FraudRiskLevel})";
        var result = shipment.MarkReassigning(reason);

        if (result.IsFailure)
        {
            logger.LogWarning(
                "Cannot mark Shipment={ShipmentId} as Reassigning: {Error}",
                shipment.Id, result.Error.Description);
            return result;
        }

        await shipmentRepository.UpdateAsync(shipment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Shipment={ShipmentId} marked Reassigning due to breakdown of Driver={DriverId}",
            shipment.Id, request.DriverId);

        return Result.Success();
    }
}
