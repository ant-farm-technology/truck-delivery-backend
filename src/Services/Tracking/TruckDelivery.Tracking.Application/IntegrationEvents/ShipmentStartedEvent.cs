using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Tracking.Application.IntegrationEvents;

// Consumer-side DTO — mirrors shape published by Shipment Service
public sealed record ShipmentStartedEvent(
    Guid ShipmentId,
    Guid OrderId,
    Guid DriverId,
    Guid VehicleId) : IntegrationEvent;
