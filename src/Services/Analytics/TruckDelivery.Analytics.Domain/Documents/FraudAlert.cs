using MongoDB.Bson.Serialization.Attributes;

namespace TruckDelivery.Analytics.Domain.Documents;

public sealed class FraudAlert
{
    [BsonId] public Guid Id { get; set; }
    public Guid OriginalDriverId { get; set; }
    public Guid ReplacementDriverId { get; set; }
    public int SwapCount { get; set; }
    public DateTime DetectedAt { get; set; }
    public bool IsAcknowledged { get; set; }

    private FraudAlert() { }

    public static FraudAlert Create(
        Guid originalDriverId, Guid replacementDriverId,
        int swapCount, DateTime detectedAt)
        => new()
        {
            Id = Guid.NewGuid(),
            OriginalDriverId = originalDriverId,
            ReplacementDriverId = replacementDriverId,
            SwapCount = swapCount,
            DetectedAt = detectedAt,
            IsAcknowledged = false
        };
}
