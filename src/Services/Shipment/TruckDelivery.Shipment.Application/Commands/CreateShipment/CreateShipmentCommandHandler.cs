using System.Text.Json;
using MediatR;
using TruckDelivery.Shipment.Application.IntegrationEvents;
using TruckDelivery.Shipment.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Shipment.Application.Commands.CreateShipment;

public sealed class CreateShipmentCommandHandler(IShipmentRepository shipmentRepository, IUnitOfWork unitOfWork, IOutboxRepository outboxRepository) : IRequestHandler<CreateShipmentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateShipmentCommand request, CancellationToken ct)
    {
        var existing = await shipmentRepository.GetByOrderIdAsync(request.OrderId, ct);
        if (existing is not null)
        {
            return Result.Failure<Guid>(Error.Conflict("Shipment", $"Shipment for order {request.OrderId} already exists."));
        }

        var shipment = Domain.Aggregates.Shipment.Create(
            request.OrderId,
            request.CustomerId,
            request.PickupCity,
            request.PickupProvince,
            request.DeliveryCity,
            request.DeliveryProvince,
            request.TotalWeightKg,
            request.TotalVolumeCbm);

        if (request.Packages is { Count: > 0 })
            shipment.StorePackages(JsonSerializer.Serialize(request.Packages));

        await shipmentRepository.AddAsync(shipment, ct);

        var @event = new ShipmentCreatedEvent(
            shipment.Id,
            shipment.OrderId,
            shipment.CustomerId,
            shipment.PickupCity,
            shipment.DeliveryCity);

        await outboxRepository.AddAsync(OutboxMessage.Create(
            eventType: nameof(ShipmentCreatedEvent),
            topic: "shipment.shipment.created",
            partitionKey: shipment.Id.ToString(),
            payload: JsonSerializer.Serialize(@event)), ct);

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(shipment.Id);
    }
}
