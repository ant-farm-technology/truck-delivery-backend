using TruckDelivery.Shipment.Domain.Aggregates;
using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Shipment.Domain.Events;

public sealed record ShipmentStatusChangedDomainEvent(
    Guid ShipmentId,
    Guid OrderId,
    ShipmentStatus OldStatus,
    ShipmentStatus NewStatus) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
