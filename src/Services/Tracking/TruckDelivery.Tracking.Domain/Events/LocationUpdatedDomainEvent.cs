using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Tracking.Domain.Events;

public sealed record LocationUpdatedDomainEvent(
    Guid SessionId,
    Guid ShipmentId,
    Guid DriverId,
    double Latitude,
    double Longitude) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
