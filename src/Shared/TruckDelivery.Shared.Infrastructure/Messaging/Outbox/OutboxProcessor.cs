using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Shared.Infrastructure.Messaging.Outbox;

public sealed class OutboxProcessor<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<OutboxProcessor<TDbContext>> logger)
    : BackgroundService
    where TDbContext : DbContext
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxProcessor<{DbContext}> started", typeof(TDbContext).Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxProcessor batch failed");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var messages = await outboxRepo.GetUnprocessedAsync(batchSize: 50, ct);
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
                logger.LogInformation("Published {EventType} Id={Id} to {Topic}", msg.EventType, msg.Id, msg.Topic);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox message {MessageId}", msg.Id);
                await outboxRepo.MarkFailedAsync(msg.Id, ex.Message, ct);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private static Headers BuildHeaders(OutboxMessage msg)
    {
        var headers = new Headers();
        headers.Add("x-event-type", Encoding.UTF8.GetBytes(msg.EventType));
        headers.Add("x-occurred-at", Encoding.UTF8.GetBytes(msg.OccurredAt.ToString("O")));

        var activity = Activity.Current;
        if (activity is not null)
            Propagator.Inject(
                new PropagationContext(activity.Context, Baggage.Current),
                headers,
                (h, key, value) => h.Add(key, Encoding.UTF8.GetBytes(value)));

        return headers;
    }
}
