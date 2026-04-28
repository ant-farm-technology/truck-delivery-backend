using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Notification.Application.IntegrationEvents;

public sealed record DriverAssignedEvent(
    Guid ShipmentId,
    Guid OrderId,
    Guid DriverId,
    Guid VehicleId) : IntegrationEvent;
