using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Driver.Application.IntegrationEvents;

// Published when fraud analysis detects a collusion pattern (>3 swaps between same pair)
// Topic: driver.fraud.suspicious-pair-detected
public sealed record SuspiciousDriverPairDetectedEvent : IntegrationEvent
{
    public Guid OriginalDriverId { get; init; }
    public Guid ReplacementDriverId { get; init; }
    public int SwapCount { get; init; }
    public DateTime DetectedAt { get; init; }
}
