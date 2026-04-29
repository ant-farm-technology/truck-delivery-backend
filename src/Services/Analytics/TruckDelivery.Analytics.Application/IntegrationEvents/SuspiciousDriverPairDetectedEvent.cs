using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Analytics.Application.IntegrationEvents;

public sealed record SuspiciousDriverPairDetectedEvent : IntegrationEvent
{
    public Guid OriginalDriverId { get; init; }
    public Guid ReplacementDriverId { get; init; }
    public int SwapCount { get; init; }
    public DateTime DetectedAt { get; init; }
}
