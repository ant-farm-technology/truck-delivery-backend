using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Shipment.Application.IntegrationEvents;

// Consumer-side DTO — mirrors the shape published by Order Service
public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    string PickupCity,
    string PickupProvince,
    string DeliveryCity,
    string DeliveryProvince,
    decimal TotalWeightKg,
    decimal TotalVolumeCbm) : IntegrationEvent;
