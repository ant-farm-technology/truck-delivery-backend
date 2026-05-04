using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using TruckDelivery.Shared.Contracts.Events;
using TruckDelivery.Shared.Infrastructure.Messaging.Kafka.Idempotency;

namespace TruckDelivery.Shared.Infrastructure.Messaging.Kafka;

public abstract class KafkaConsumerBase<TEvent>(IConsumer<string, string> consumer, IIdempotencyStore idempotencyStore, IMediator mediator, ILogger logger, string topic) : BackgroundService
    where TEvent : IntegrationEvent
{
    private static readonly ActivitySource ActivitySource = new("TruckDelivery.Kafka.Consumer");
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    protected abstract IRequest CreateCommand(TEvent @event);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe(topic);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Kafka consumer subscribed to {Topic}", topic);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                await ProcessAsync(result, stoppingToken);
                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error consuming from {Topic}", topic);
            }
        }

        consumer.Close();
    }

    private async Task ProcessAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        var parentContext = Propagator.Extract(default, result.Message.Headers,
            (headers, key) =>
            {
                var header = headers.FirstOrDefault(h => h.Key == key);
                return header is null ? [] : [Encoding.UTF8.GetString(header.GetValueBytes())];
            });

        Baggage.Current = parentContext.Baggage;

        using var activity = ActivitySource.StartActivity($"consume {topic}", ActivityKind.Consumer, parentContext.ActivityContext);

        var @event = JsonSerializer.Deserialize<TEvent>(result.Message.Value);
        if (@event is null)
        {
            logger.LogWarning("Failed to deserialize message from {Topic}", topic);
            return;
        }

        if (await idempotencyStore.HasProcessedAsync(@event.MessageId, ct))
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Skipping duplicate MessageId={MessageId}", @event.MessageId);
            }
            return;
        }

        var command = CreateCommand(@event);
        await mediator.Send(command, ct);
        await idempotencyStore.MarkProcessedAsync(@event.MessageId, ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Processed {EventType} MessageId={MessageId}", typeof(TEvent).Name, @event.MessageId);
        }
    }
}
