using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Payment.Domain.Events;

public sealed record PaymentCreatedDomainEvent(Guid PaymentId, Guid OrderId, Guid CustomerId, decimal Amount) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
