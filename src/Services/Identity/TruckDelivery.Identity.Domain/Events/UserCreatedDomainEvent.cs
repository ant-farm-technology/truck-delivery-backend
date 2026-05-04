using TruckDelivery.Shared.Common.Domain;
using TruckDelivery.Identity.Domain.ValueObjects;

namespace TruckDelivery.Identity.Domain.Events;

public sealed record UserCreatedDomainEvent(Guid UserId, string Email, UserRole Role) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
