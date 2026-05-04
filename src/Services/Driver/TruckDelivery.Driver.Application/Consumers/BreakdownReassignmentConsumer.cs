using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using TruckDelivery.Driver.Application.Commands.RecordDriverSwap;
using TruckDelivery.Driver.Application.IntegrationEvents;
using TruckDelivery.Shared.Infrastructure.Messaging.Kafka.Idempotency;

namespace TruckDelivery.Driver.Application.Consumers;

// Consumes BreakdownReassignmentCompletedEvent from Shipment Service
// Records driver swap pairs for collusion analysis
public sealed class BreakdownReassignmentConsumer(
    ConsumerConfig consumerConfig,
    IProducer<string, string> producer,
    IIdempotencyStore idempotencyStore,
    IMediator mediator,
    ILogger<BreakdownReassignmentConsumer> logger) : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("TruckDelivery.Kafka.Consumer");
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    private const string Topic = "shipment.breakdown.reassignment-completed";
    private const string DlqTopic = "shipment.breakdown.reassignment-completed.dlq";

    private readonly IConsumer<string, string> _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(Topic);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Subscribed to {Topic}", Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = _consumer.Consume(stoppingToken);
                await ProcessAsync(result, stoppingToken);
                _consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process message from {Topic}", Topic);
                if (result is not null)
                {
                    await RouteToDlqAsync(result, ex, stoppingToken);
                    _consumer.Commit(result);
                }
            }
        }

        _consumer.Close();
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
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

        using var activity = ActivitySource.StartActivity($"consume {Topic}", ActivityKind.Consumer, parentContext.ActivityContext);

        var @event = JsonSerializer.Deserialize<BreakdownReassignmentCompletedEvent>(result.Message.Value);
        if (@event is null)
        {
            await RouteToDlqAsync(result, new InvalidOperationException("Deserialization failed"), ct);
            return;
        }

        if (await idempotencyStore.HasProcessedAsync(@event.MessageId, ct))
        {
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Skipping duplicate MessageId={MessageId}", @event.MessageId);
            return;
        }

        var command = new RecordDriverSwapCommand(
            @event.OriginalDriverId,
            @event.ReplacementDriverId,
            @event.ShipmentId);

        await mediator.Send(command, ct);
        await idempotencyStore.MarkProcessedAsync(@event.MessageId, ct);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Recorded swap: Original={OriginalDriverId} Replacement={ReplacementDriverId} Shipment={ShipmentId}",
                @event.OriginalDriverId, @event.ReplacementDriverId, @event.ShipmentId);
    }

    private async Task RouteToDlqAsync(ConsumeResult<string, string> result, Exception ex, CancellationToken ct)
    {
        try
        {
            var dlqHeaders = new Headers();
            foreach (var h in result.Message.Headers)
                dlqHeaders.Add(h.Key, h.GetValueBytes());
            dlqHeaders.Add("x-dlq-reason", Encoding.UTF8.GetBytes(ex.Message));
            dlqHeaders.Add("x-dlq-source-topic", Encoding.UTF8.GetBytes(Topic));
            dlqHeaders.Add("x-dlq-timestamp", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")));

            await producer.ProduceAsync(DlqTopic, new Message<string, string>
            {
                Key = result.Message.Key,
                Value = result.Message.Value,
                Headers = dlqHeaders
            }, ct);

            logger.LogWarning("Routed to DLQ topic={DlqTopic}", DlqTopic);
        }
        catch (Exception dlqEx)
        {
            logger.LogCritical(dlqEx, "Failed to route to DLQ — message will be lost! Key={Key}", result.Message.Key);
        }
    }
}
