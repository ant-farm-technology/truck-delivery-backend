using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using TruckDelivery.Driver.Application.Commands.RegisterDriver;
using TruckDelivery.Shared.Contracts.Events;
using TruckDelivery.Shared.Infrastructure.Messaging.Kafka.Idempotency;

namespace TruckDelivery.Driver.Application.Consumers;

public sealed class UserRegisteredConsumer(
    IConsumer<string, string> consumer,
    IIdempotencyStore idempotencyStore,
    IMediator mediator,
    ILogger<UserRegisteredConsumer> logger)
    : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("TruckDelivery.Kafka.Consumer");
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    private const string Topic = "userregistered";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe(Topic);
        logger.LogInformation("Kafka consumer subscribed to {Topic}", Topic);

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
                logger.LogError(ex, "Unhandled error consuming from {Topic}", Topic);
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

        using var activity = ActivitySource.StartActivity(
            $"consume {Topic}", ActivityKind.Consumer, parentContext.ActivityContext);

        var @event = JsonSerializer.Deserialize<UserRegisteredEvent>(result.Message.Value);
        if (@event is null)
        {
            logger.LogWarning("Failed to deserialize UserRegisteredEvent");
            return;
        }

        if (!string.Equals(@event.Role, "Driver", StringComparison.OrdinalIgnoreCase))
            return;

        if (await idempotencyStore.HasProcessedAsync(@event.MessageId, ct))
        {
            logger.LogInformation("Skipping duplicate MessageId={MessageId}", @event.MessageId);
            return;
        }

        var command = new RegisterDriverCommand(
            @event.UserId,
            @event.Email,
            @event.FirstName,
            @event.LastName,
            PhoneNumber: string.Empty,
            LicenseNumber: string.Empty);

        await mediator.Send(command, ct);
        await idempotencyStore.MarkProcessedAsync(@event.MessageId, ct);

        logger.LogInformation("Registered driver profile for UserId={UserId}", @event.UserId);
    }
}
