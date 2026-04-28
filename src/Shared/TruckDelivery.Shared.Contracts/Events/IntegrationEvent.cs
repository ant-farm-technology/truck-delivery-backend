namespace TruckDelivery.Shared.Contracts.Events;

public abstract record IntegrationEvent
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public int SchemaVersion { get; init; } = 1;
}
