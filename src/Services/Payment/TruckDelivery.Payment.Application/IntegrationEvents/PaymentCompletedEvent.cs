using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Payment.Application.IntegrationEvents;

public sealed record PaymentCompletedEvent(
    Guid PaymentId,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency = "VND") : IntegrationEvent;
