using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Order.Domain.Events;

public sealed record OrderCreatedDomainEvent(Guid OrderId, Guid CustomerId, decimal TotalWeightKg) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
