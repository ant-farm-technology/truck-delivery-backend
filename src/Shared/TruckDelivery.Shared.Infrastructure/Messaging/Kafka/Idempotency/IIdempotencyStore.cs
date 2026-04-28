namespace TruckDelivery.Shared.Infrastructure.Messaging.Kafka.Idempotency;

public interface IIdempotencyStore
{
    Task<bool> HasProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);
}
