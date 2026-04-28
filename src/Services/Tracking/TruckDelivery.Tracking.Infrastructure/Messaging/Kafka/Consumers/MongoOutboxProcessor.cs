using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Tracking.Infrastructure.Messaging.Kafka.Consumers;

public sealed class MongoOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<MongoOutboxProcessor> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MongoOutboxProcessor started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessBatchAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "MongoOutboxProcessor batch failed"); }
            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var producer = scope.ServiceProvider.GetRequiredService<IProducer<string, string>>();

        var messages = await outboxRepo.GetUnprocessedAsync(50, ct);
        if (messages.Count == 0) return;

        foreach (var msg in messages)
        {
            try
            {
                using var activity = new Activity("outbox.publish").Start();
                var headers = BuildHeaders(msg);

                await producer.ProduceAsync(msg.Topic, new Message<string, string>
                {
                    Key = msg.PartitionKey,
                    Value = msg.Payload,
                    Headers = headers
                }, ct);

                await outboxRepo.MarkProcessedAsync(msg.Id, ct);
                logger.LogInformation("Published {EventType} MessageId={Id}", msg.EventType, msg.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox message {MessageId}", msg.Id);
                await outboxRepo.MarkFailedAsync(msg.Id, ex.Message, ct);
            }
        }
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
