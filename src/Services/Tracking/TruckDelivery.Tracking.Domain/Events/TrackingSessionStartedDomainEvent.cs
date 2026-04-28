using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Tracking.Domain.Events;

public sealed record TrackingSessionStartedDomainEvent(
    Guid SessionId,
    Guid ShipmentId,
    Guid DriverId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
