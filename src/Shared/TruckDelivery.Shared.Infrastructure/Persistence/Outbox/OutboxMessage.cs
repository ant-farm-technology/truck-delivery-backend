namespace TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public Guid Id { get; private set; }
    public string EventType { get; private set; } = null!;
    public string Topic { get; private set; } = null!;
    public string PartitionKey { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTime OccurredAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }

    public static OutboxMessage Create(string eventType, string topic, string partitionKey, string payload)
        => new()
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Topic = topic,
            PartitionKey = partitionKey,
            Payload = payload,
            OccurredAt = DateTime.UtcNow
        };

    public void MarkProcessed() => ProcessedAt = DateTime.UtcNow;

    public void MarkFailed(string error)
    {
        RetryCount++;
        LastError = error;
    }
}
