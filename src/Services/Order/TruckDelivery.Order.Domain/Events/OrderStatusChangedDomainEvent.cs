using TruckDelivery.Shared.Common.Domain;
using TruckDelivery.Order.Domain.ValueObjects;

namespace TruckDelivery.Order.Domain.Events;

public sealed record OrderStatusChangedDomainEvent(Guid OrderId, OrderStatus OldStatus, OrderStatus NewStatus) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
