using System.Text.Json;
using MediatR;
using TruckDelivery.Shipment.Application.IntegrationEvents;
using TruckDelivery.Shipment.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Shipment.Application.Commands.FailShipment;

public sealed class FailShipmentCommandHandler(IShipmentRepository shipmentRepository, IUnitOfWork unitOfWork, IOutboxRepository outboxRepository) : IRequestHandler<FailShipmentCommand, Result>
{
    public async Task<Result> Handle(FailShipmentCommand request, CancellationToken ct)
    {
        var shipment = await shipmentRepository.GetByIdAsync(request.ShipmentId, ct);
        if (shipment is null)
        {
            return Result.Failure(Error.NotFound("Shipment", $"Shipment {request.ShipmentId} not found."));
        }

        shipment.Fail(request.Reason);

        var @event = new ShipmentFailedEvent(shipment.Id, shipment.OrderId, request.Reason);
        await outboxRepository.AddAsync(OutboxMessage.Create(
            nameof(ShipmentFailedEvent),
            "shipment.shipment.failed",
            shipment.Id.ToString(),
            JsonSerializer.Serialize(@event)), ct);

        await shipmentRepository.UpdateAsync(shipment, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
