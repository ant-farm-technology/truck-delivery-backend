namespace TruckDelivery.Notification.Application.IntegrationEvents;

// Consumer-side DTO for topic: driver.driver.manual-review-required
public sealed record DriverManualReviewRequiredEvent
{
    public Guid MessageId { get; init; }
    public Guid DriverId { get; init; }
    public string DriverName { get; init; } = default!;
    public float ConfidenceScore { get; init; }
    public string? Notes { get; init; }
}
