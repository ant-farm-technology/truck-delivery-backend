namespace TruckDelivery.Shared.Contracts.Events;

public sealed record UserRegisteredEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Role) : IntegrationEvent;
