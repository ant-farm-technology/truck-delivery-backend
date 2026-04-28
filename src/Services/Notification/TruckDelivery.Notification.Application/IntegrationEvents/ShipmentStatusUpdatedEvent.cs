using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Notification.Application.IntegrationEvents;

public sealed record ShipmentStatusUpdatedEvent(
    Guid ShipmentId,
    Guid OrderId,
    Guid CustomerId,
    string Status,
    Guid? DriverId) : IntegrationEvent;
