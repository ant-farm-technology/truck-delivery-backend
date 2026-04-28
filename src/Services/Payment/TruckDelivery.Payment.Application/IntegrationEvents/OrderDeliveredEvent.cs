using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Payment.Application.IntegrationEvents;

public sealed record OrderDeliveredEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalFee,
    string Currency = "VND") : IntegrationEvent;
