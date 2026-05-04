using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Order.Application.IntegrationEvents;

public sealed record OrderCancelledEvent(
    Guid OrderId,
    Guid CustomerId,
    string Reason) : IntegrationEvent;
