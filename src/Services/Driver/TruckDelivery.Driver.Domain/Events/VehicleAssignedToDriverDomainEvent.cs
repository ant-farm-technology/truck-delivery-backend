using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Driver.Domain.Events;

public sealed record VehicleAssignedToDriverDomainEvent(Guid VehicleId, Guid DriverId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
