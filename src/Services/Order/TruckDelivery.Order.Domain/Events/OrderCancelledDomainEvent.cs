using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Order.Domain.Events;

public sealed record OrderCancelledDomainEvent(Guid OrderId, Guid CustomerId, string Reason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
