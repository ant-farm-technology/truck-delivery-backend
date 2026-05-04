using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Tracking.Application.IntegrationEvents;

public sealed record ShipmentCompletedEvent(
    Guid ShipmentId,
    Guid OrderId,
    Guid CustomerId,
    Guid DriverId) : IntegrationEvent;
