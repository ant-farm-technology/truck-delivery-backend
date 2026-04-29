using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Driver.Domain.Aggregates;

public sealed class DriverSwapRecord : Entity<Guid>
{
    private DriverSwapRecord() { }

    private DriverSwapRecord(Guid id) : base(id) { }

    public Guid OriginalDriverId { get; private set; }
    public Guid ReplacementDriverId { get; private set; }
    public Guid ShipmentId { get; private set; }
    public DateTime OccurredAt { get; private set; }

    public static DriverSwapRecord Create(Guid originalDriverId, Guid replacementDriverId, Guid shipmentId)
        => new(Guid.NewGuid())
        {
            OriginalDriverId = originalDriverId,
            ReplacementDriverId = replacementDriverId,
            ShipmentId = shipmentId,
            OccurredAt = DateTime.UtcNow
        };
}
