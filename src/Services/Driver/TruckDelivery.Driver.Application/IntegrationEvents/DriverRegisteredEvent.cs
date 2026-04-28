using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Driver.Application.IntegrationEvents;

public sealed record DriverRegisteredEvent(
    Guid DriverId,
    string Email,
    string FullName,
    string PhoneNumber) : IntegrationEvent;
