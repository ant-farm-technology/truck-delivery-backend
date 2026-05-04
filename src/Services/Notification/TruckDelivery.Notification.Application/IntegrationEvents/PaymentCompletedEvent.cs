using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Notification.Application.IntegrationEvents;

public sealed record PaymentCompletedEvent(
    Guid PaymentId,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount) : IntegrationEvent;
