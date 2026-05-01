using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Driver.Application.IntegrationEvents;

// Published when OCR confidence is 0.65–0.85 → driver needs admin manual review
// Topic: driver.driver.manual-review-required
public sealed record DriverManualReviewRequiredEvent : IntegrationEvent
{
    public Guid DriverId { get; init; }
    public string DriverName { get; init; } = default!;
    public float ConfidenceScore { get; init; }
    public string? Notes { get; init; }
}
