using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Shipment.Application.IntegrationEvents;

public sealed record ShipmentFailedEvent(
    Guid ShipmentId,
    Guid OrderId,
    string Reason) : IntegrationEvent;
