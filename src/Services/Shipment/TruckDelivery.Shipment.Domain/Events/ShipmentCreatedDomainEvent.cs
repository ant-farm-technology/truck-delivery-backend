using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Shipment.Domain.Events;

public sealed record ShipmentCreatedDomainEvent(
    Guid ShipmentId,
    Guid OrderId,
    Guid CustomerId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
