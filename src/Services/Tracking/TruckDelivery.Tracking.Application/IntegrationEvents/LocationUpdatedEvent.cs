using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Tracking.Application.IntegrationEvents;

public sealed record LocationUpdatedEvent(
    Guid ShipmentId,
    Guid DriverId,
    double Latitude,
    double Longitude,
    double? SpeedKmh) : IntegrationEvent;
