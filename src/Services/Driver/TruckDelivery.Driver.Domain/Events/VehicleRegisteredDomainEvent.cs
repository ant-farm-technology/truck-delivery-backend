using TruckDelivery.Shared.Common.Domain;
using TruckDelivery.Driver.Domain.ValueObjects;

namespace TruckDelivery.Driver.Domain.Events;

public sealed record VehicleRegisteredDomainEvent(Guid VehicleId, string LicensePlate, VehicleType Type) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
