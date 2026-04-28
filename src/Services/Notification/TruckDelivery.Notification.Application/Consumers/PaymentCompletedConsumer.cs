using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using TruckDelivery.Notification.Application.Commands.SendNotification;
using TruckDelivery.Notification.Application.IntegrationEvents;
using TruckDelivery.Notification.Domain.Aggregates;
using TruckDelivery.Shared.Infrastructure.Messaging.Kafka.Idempotency;

namespace TruckDelivery.Notification.Application.Consumers;

public sealed class PaymentCompletedConsumer(
    ConsumerConfig consumerConfig,
    IProducer<string, string> producer,
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentCompletedConsumer> logger)
    : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("TruckDelivery.Kafka.Consumer");
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    private const string Topic = "payment.payment.completed";
    private const string DlqTopic = "payment.payment.completed.dlq";

    private readonly IConsumer<string, string> _consumer =
        new ConsumerBuilder<string, string>(consumerConfig).Build();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(Topic);
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
            catch (OperationCanceledException) { break; }
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

    private async Task ProcessAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        var parentContext = Propagator.Extract(default, result.Message.Headers,
            (headers, key) =>
            {
                var h = headers.FirstOrDefault(x => x.Key == key);
                return h is null ? [] : [Encoding.UTF8.GetString(h.GetValueBytes())];
            });
        Baggage.Current = parentContext.Baggage;

        using var activity = ActivitySource.StartActivity(
            $"consume {Topic}", ActivityKind.Consumer, parentContext.ActivityContext);

        var @event = JsonSerializer.Deserialize<PaymentCompletedEvent>(result.Message.Value);
        if (@event is null)
        {
            await RouteToDlqAsync(result, new InvalidOperationException("Deserialization failed"), ct);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var idempotencyStore = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

        if (await idempotencyStore.HasProcessedAsync(@event.MessageId, ct))
        {
            logger.LogInformation("Skipping duplicate MessageId={MessageId}", @event.MessageId);
            return;
        }

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(new SendNotificationCommand(
            RecipientId: @event.CustomerId,
            Type: NotificationType.PaymentCompleted,
            Channels: [NotificationChannel.Push, NotificationChannel.Email],
            Title: "Payment successful",
            Body: $"Payment of {(@event.Amount):C} for order {@event.OrderId} completed successfully."), ct);

        await idempotencyStore.MarkProcessedAsync(@event.MessageId, ct);
        logger.LogInformation("Notification sent for PaymentCompleted OrderId={OrderId}", @event.OrderId);
    }

    private async Task RouteToDlqAsync(ConsumeResult<string, string> result, Exception ex, CancellationToken ct)
    {
        try
        {
            var dlqHeaders = new Headers();
            foreach (var h in result.Message.Headers) dlqHeaders.Add(h.Key, h.GetValueBytes());
            dlqHeaders.Add("x-dlq-reason", Encoding.UTF8.GetBytes(ex.Message));
            dlqHeaders.Add("x-dlq-source-topic", Encoding.UTF8.GetBytes(Topic));
            dlqHeaders.Add("x-dlq-timestamp", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")));
            await producer.ProduceAsync(DlqTopic, new Message<string, string>
            {
                Key = result.Message.Key,
                Value = result.Message.Value,
                Headers = dlqHeaders
            }, ct);
        }
        catch (Exception dlqEx)
        {
            logger.LogCritical(dlqEx, "Failed to route to DLQ — message will be lost!");
        }
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}
