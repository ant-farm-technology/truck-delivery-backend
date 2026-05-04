using MediatR;

namespace TruckDelivery.Shared.Common.Domain;

public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}
