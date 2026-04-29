using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Driver.Domain.Events;

public sealed record TrustScoreUpdatedDomainEvent(
    Guid DriverId,
    int NewScore,
    int Delta,
    string Reason) : IDomainEvent;
