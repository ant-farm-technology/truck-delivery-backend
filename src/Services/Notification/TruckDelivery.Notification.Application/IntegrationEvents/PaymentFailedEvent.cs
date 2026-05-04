using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Notification.Application.IntegrationEvents;

public sealed record PaymentFailedEvent(
    Guid PaymentId,
    Guid OrderId,
    Guid CustomerId,
    string FailureReason) : IntegrationEvent;
