using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Order.Application.Consumers.IntegrationEvents;

// Incoming event from Shipment service when delivery is confirmed complete
public sealed record ShipmentCompletedEvent(
    Guid ShipmentId,
    Guid OrderId,
    Guid CustomerId,
    Guid DriverId) : IntegrationEvent;
