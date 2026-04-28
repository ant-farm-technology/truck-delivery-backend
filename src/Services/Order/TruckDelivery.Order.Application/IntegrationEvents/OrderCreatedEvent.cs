using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Order.Application.IntegrationEvents;

public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    string PickupCity,
    string PickupProvince,
    string DeliveryCity,
    string DeliveryProvince,
    decimal TotalWeightKg,
    decimal TotalVolumeCbm) : IntegrationEvent;
