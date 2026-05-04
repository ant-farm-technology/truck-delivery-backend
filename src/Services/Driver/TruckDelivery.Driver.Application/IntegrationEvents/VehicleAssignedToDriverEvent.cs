using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Driver.Application.IntegrationEvents;

public sealed record VehicleAssignedToDriverEvent(
    Guid VehicleId,
    Guid DriverId,
    string VehicleType,
    decimal MaxWeightKg) : IntegrationEvent;
