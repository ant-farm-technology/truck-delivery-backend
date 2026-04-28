using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Payment.Application.IntegrationEvents;

public sealed record PaymentFailedEvent(
    Guid PaymentId,
    Guid OrderId,
    string Reason) : IntegrationEvent;
