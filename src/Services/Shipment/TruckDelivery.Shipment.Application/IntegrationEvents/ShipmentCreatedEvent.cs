using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Shipment.Application.IntegrationEvents;

public sealed record ShipmentCreatedEvent(
    Guid ShipmentId,
    Guid OrderId,
    Guid CustomerId,
    string PickupCity,
    string DeliveryCity) : IntegrationEvent;
