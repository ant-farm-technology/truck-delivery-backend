using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Shipment.Application.IntegrationEvents;

// Consumer-side DTO — published by Driver Service after driver assignment confirmed
public sealed record DriverAssignedEvent(
    Guid ShipmentId,
    Guid OrderId,
    Guid DriverId,
    Guid VehicleId,
    decimal VehicleMaxWeightKg = 0,
    decimal? VehicleLengthM = null,
    decimal? VehicleWidthM = null,
    decimal? VehicleHeightM = null) : IntegrationEvent;
