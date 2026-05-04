using System.Text.Json;
using MediatR;
using TruckDelivery.Shipment.Application.IntegrationEvents;
using TruckDelivery.Shipment.Domain.Aggregates;
using TruckDelivery.Shipment.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Shipment.Application.Commands.UpdateShipmentStatus;

public sealed class UpdateShipmentStatusCommandHandler(IShipmentRepository shipmentRepository, IUnitOfWork unitOfWork, IOutboxRepository outboxRepository) : IRequestHandler<UpdateShipmentStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateShipmentStatusCommand request, CancellationToken ct)
    {
        var shipment = await shipmentRepository.GetByIdAsync(request.ShipmentId, ct);
        if (shipment is null)
        {
            return Result.Failure(Error.NotFound("Shipment", $"Shipment {request.ShipmentId} not found."));
        }

        var result = shipment.TransitionTo(request.NewStatus);
        if (result.IsFailure)
        {
            return result;
        }

        if (request.NewStatus == ShipmentStatus.Completed)
        {
            var completedEvent = new ShipmentCompletedEvent(
                shipment.Id,
                shipment.OrderId,
                shipment.CustomerId,
                shipment.AssignedDriverId!.Value);

            await outboxRepository.AddAsync(OutboxMessage.Create(
                nameof(ShipmentCompletedEvent),
                "shipment.shipment.completed",
                shipment.Id.ToString(),
                JsonSerializer.Serialize(completedEvent)), ct);
        }

        await shipmentRepository.UpdateAsync(shipment, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
