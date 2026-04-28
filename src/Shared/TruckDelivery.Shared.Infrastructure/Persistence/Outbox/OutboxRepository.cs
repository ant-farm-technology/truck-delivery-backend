using Microsoft.EntityFrameworkCore;

namespace TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

public sealed class OutboxRepository<TDbContext>(TDbContext ctx) : IOutboxRepository
    where TDbContext : DbContext
{
    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default)
        => await ctx.Set<OutboxMessage>().AddAsync(message, ct);

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken ct = default)
        => await ctx.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.OccurredAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task MarkProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        var msg = await ctx.Set<OutboxMessage>().FindAsync([messageId], ct)
            ?? throw new InvalidOperationException($"OutboxMessage {messageId} not found");
        msg.MarkProcessed();
    }

    public async Task MarkFailedAsync(Guid messageId, string error, CancellationToken ct = default)
    {
        var msg = await ctx.Set<OutboxMessage>().FindAsync([messageId], ct)
            ?? throw new InvalidOperationException($"OutboxMessage {messageId} not found");
        msg.MarkFailed(error);
    }
}
