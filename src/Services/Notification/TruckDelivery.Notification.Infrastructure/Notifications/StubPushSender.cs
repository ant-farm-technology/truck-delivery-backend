using Microsoft.Extensions.Logging;
using TruckDelivery.Notification.Application.Interfaces;

namespace TruckDelivery.Notification.Infrastructure.Notifications;

// TODO: Replace with real FCM implementation (Firebase Admin SDK) when push credentials are available.
public sealed class StubPushSender(ILogger<StubPushSender> logger) : IPushNotificationSender
{
    public Task SendAsync(Guid recipientId, string title, string body, CancellationToken ct = default)
    {
        logger.LogInformation("[PUSH-STUB] To={RecipientId} Title={Title}", recipientId, title);
        return Task.CompletedTask;
    }
}
