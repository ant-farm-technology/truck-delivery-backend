# /scaffold-outbox — Scaffold Outbox Pattern for a Service

Scaffold hoàn chỉnh Outbox pattern (Transactional Outbox) để đảm bảo at-least-once delivery cho Kafka events.

**Arguments:** `$ARGUMENTS` = tên service (ví dụ: `Order`, `Shipment`)

## Yêu cầu

Tạo các files sau cho service `$ARGUMENTS`:

### 1. Infrastructure/Persistence/EFCore/OutboxMessage.cs

```csharp
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
```

### 2. Infrastructure/Persistence/EFCore/Configurations/OutboxMessageConfiguration.cs

```csharp
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Topic).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PartitionKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("longtext").IsRequired();
        builder.HasIndex(x => x.ProcessedAt);
        builder.HasIndex(x => new { x.ProcessedAt, x.RetryCount });
        builder.ToTable("OutboxMessages");
    }
}
```

### 3. Infrastructure/Persistence/Outbox/IOutboxMessageRepository.cs

```csharp
public interface IOutboxMessageRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
    Task<IEnumerable<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken ct = default);
    Task MarkProcessedAsync(Guid messageId, CancellationToken ct = default);
    Task MarkFailedAsync(Guid messageId, string error, CancellationToken ct = default);
}
```

### 4. Infrastructure/Persistence/Outbox/OutboxMessageRepository.cs

```csharp
public sealed class OutboxMessageRepository : IOutboxMessageRepository
{
    private readonly $ARGUMENTSDbContext _ctx;

    public OutboxMessageRepository($ARGUMENTSDbContext ctx) => _ctx = ctx;

    public async Task AddAsync(OutboxMessage message, CancellationToken ct)
        => await _ctx.OutboxMessages.AddAsync(message, ct);

    public async Task<IEnumerable<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken ct)
        => await _ctx.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.OccurredAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task MarkProcessedAsync(Guid messageId, CancellationToken ct)
    {
        var msg = await _ctx.OutboxMessages.FindAsync([messageId], ct)
            ?? throw new InvalidOperationException($"OutboxMessage {messageId} not found");
        msg.MarkProcessed();
    }

    public async Task MarkFailedAsync(Guid messageId, string error, CancellationToken ct)
    {
        var msg = await _ctx.OutboxMessages.FindAsync([messageId], ct)
            ?? throw new InvalidOperationException($"OutboxMessage {messageId} not found");
        msg.MarkFailed(error);
    }
}
```

### 5. Infrastructure/Messaging/Outbox/OutboxProcessor.cs (BackgroundService)

```csharp
public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessBatchAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "OutboxProcessor batch failed"); }
            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var producer = scope.ServiceProvider.GetRequiredService<IProducer<string, string>>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var messages = (await outboxRepo.GetUnprocessedAsync(batchSize: 50, ct)).ToList();
        if (messages.Count == 0) return;

        foreach (var msg in messages)
        {
            try
            {
                await producer.ProduceAsync(msg.Topic, new Message<string, string>
                {
                    Key = msg.PartitionKey,
                    Value = msg.Payload,
                    Headers = BuildHeaders(msg)
                }, ct);

                await outboxRepo.MarkProcessedAsync(msg.Id, ct);
                _logger.LogInformation("Published {EventType} MessageId={Id} to {Topic}",
                    msg.EventType, msg.Id, msg.Topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish outbox message {MessageId}", msg.Id);
                await outboxRepo.MarkFailedAsync(msg.Id, ex.Message, ct);
            }
        }
        await uow.CommitAsync(ct);
    }

    private static Headers BuildHeaders(OutboxMessage msg)
    {
        var headers = new Headers();
        headers.Add("x-event-type", Encoding.UTF8.GetBytes(msg.EventType));
        headers.Add("x-occurred-at", Encoding.UTF8.GetBytes(msg.OccurredAt.ToString("O")));
        var activity = Activity.Current;
        if (activity is not null)
            Propagators.DefaultTextMapPropagator.Inject(
                new PropagationContext(activity.Context, Baggage.Current),
                headers,
                (h, key, value) => h.Add(key, Encoding.UTF8.GetBytes(value)));
        return headers;
    }
}
```

### 6. Program.cs Registration

```csharp
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
// Thêm vào DbContext: public DbSet<OutboxMessage> OutboxMessages { get; set; }
```

### 7. EF Migration

```bash
cd src/Services/$ARGUMENTS/$ARGUMENTS.Infrastructure
dotnet ef migrations add AddOutboxMessages --project . --startup-project ../$ARGUMENTS.Api
```

## Rules

- OutboxMessage lưu trong **cùng DB transaction** với business entity
- KHÔNG publish Kafka trực tiếp từ Command handler — phải qua Outbox
- OutboxProcessor poll mỗi 5s (BackgroundService)
- Max retry = 5 (alert + skip sau đó)
- Processed messages giữ lại để audit (soft delete sau 7 ngày)
