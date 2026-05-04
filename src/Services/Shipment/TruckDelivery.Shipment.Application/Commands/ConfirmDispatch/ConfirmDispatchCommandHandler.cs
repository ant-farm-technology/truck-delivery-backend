using System.Text.Json;
using MediatR;
using TruckDelivery.Shipment.Application.IntegrationEvents;
using TruckDelivery.Shipment.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Shipment.Application.Commands.ConfirmDispatch;

public sealed class ConfirmDispatchCommandHandler(
    IShipmentRepository shipmentRepository,
    IOutboxRepository outboxRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ConfirmDispatchCommand, Result>
{
    public async Task<Result> Handle(ConfirmDispatchCommand request, CancellationToken ct)
    {
        var shipment = await shipmentRepository.GetByIdAsync(request.ShipmentId, ct);
        if (shipment is null)
            return Result.Failure(Error.NotFound("Shipment", $"Shipment {request.ShipmentId} not found."));

        var confirmResult = shipment.ConfirmByDispatcher();
        if (confirmResult.IsFailure)
            return confirmResult;

        var startedEvent = new ShipmentStartedEvent(
            shipment.Id,
            shipment.OrderId,
            shipment.AssignedDriverId!.Value,
            shipment.AssignedVehicleId!.Value);

        await outboxRepository.AddAsync(OutboxMessage.Create(
            nameof(ShipmentStartedEvent),
            "shipment.shipment.started",
            shipment.Id.ToString(),
            JsonSerializer.Serialize(startedEvent)), ct);

        await shipmentRepository.UpdateAsync(shipment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
