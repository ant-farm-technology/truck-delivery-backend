using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Order.Application.Consumers.IntegrationEvents;

// Incoming event from Payment service after COD payment is processed
public sealed record PaymentCompletedEvent(
    Guid PaymentId,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency = "VND") : IntegrationEvent;
