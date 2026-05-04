using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Order.Application.Consumers.IntegrationEvents;

// Incoming event from Shipment service after driver is confirmed
public sealed record DriverAssignedEvent(
    Guid ShipmentId,
    Guid OrderId,
    Guid DriverId,
    Guid VehicleId) : IntegrationEvent;
