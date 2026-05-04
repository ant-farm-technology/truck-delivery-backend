using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Payment.Domain.Events;

public sealed record PaymentFailedDomainEvent(Guid PaymentId, Guid OrderId, string Reason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
