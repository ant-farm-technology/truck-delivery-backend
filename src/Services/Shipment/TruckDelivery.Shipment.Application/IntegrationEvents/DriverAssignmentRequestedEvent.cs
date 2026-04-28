using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Shipment.Application.IntegrationEvents;

public sealed record DriverAssignmentRequestedEvent(
    Guid ShipmentId,
    Guid OrderId,
    decimal TotalWeightKg,
    decimal TotalVolumeCbm,
    double DistanceMeters) : IntegrationEvent;
