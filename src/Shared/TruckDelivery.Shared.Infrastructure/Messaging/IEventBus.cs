using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Shared.Infrastructure.Messaging;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, string topic, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent;
}
