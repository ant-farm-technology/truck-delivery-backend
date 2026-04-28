using TruckDelivery.Shared.Common.Domain;
using TruckDelivery.Driver.Domain.ValueObjects;

namespace TruckDelivery.Driver.Domain.Events;

public sealed record DriverStatusChangedDomainEvent(Guid DriverId, DriverStatus OldStatus, DriverStatus NewStatus) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
