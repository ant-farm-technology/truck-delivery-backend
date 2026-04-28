using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Shipment.Application.IntegrationEvents;

public sealed record ShipmentStartedEvent(
    Guid ShipmentId,
    Guid OrderId,
    Guid DriverId,
    Guid VehicleId) : IntegrationEvent;
