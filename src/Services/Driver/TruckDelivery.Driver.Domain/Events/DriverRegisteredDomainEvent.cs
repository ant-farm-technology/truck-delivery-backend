using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Driver.Domain.Events;

public sealed record DriverRegisteredDomainEvent(Guid DriverId, string Email, string PhoneNumber) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
