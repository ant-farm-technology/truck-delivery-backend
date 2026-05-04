using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Driver.Application.IntegrationEvents;

public sealed record DriverStatusChangedEvent(
    Guid DriverId,
    string OldStatus,
    string NewStatus,
    Guid? CurrentVehicleId) : IntegrationEvent;
