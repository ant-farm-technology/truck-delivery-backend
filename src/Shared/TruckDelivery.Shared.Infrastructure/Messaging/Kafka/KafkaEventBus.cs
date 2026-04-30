using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Shared.Infrastructure.Messaging.Kafka;

public sealed class KafkaEventBus(IProducer<string, string> producer, ILogger<KafkaEventBus> logger) : IEventBus
{
    private static readonly ActivitySource ActivitySource = new("TruckDelivery.Kafka.Producer");
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public async Task PublishAsync<TEvent>(TEvent @event, string topic, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        using var activity = ActivitySource.StartActivity($"publish {topic}", ActivityKind.Producer);

        var headers = new Headers();
        Propagator.Inject(
            new PropagationContext(activity?.Context ?? default, Baggage.Current),
            headers,
            (h, key, value) => h.Add(key, Encoding.UTF8.GetBytes(value)));

        var message = new Message<string, string>
        {
            Key = @event.MessageId.ToString(),
            Value = JsonSerializer.Serialize(@event),
            Headers = headers
        };

        try
        {
            await producer.ProduceAsync(topic, message, cancellationToken);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Published {EventType} MessageId={MessageId} Topic={Topic}", typeof(TEvent).Name, @event.MessageId, topic);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish {EventType} MessageId={MessageId}", typeof(TEvent).Name, @event.MessageId);
            throw;
        }
    }

}
