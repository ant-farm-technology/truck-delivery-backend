using Microsoft.Extensions.Logging;
using TruckDelivery.Notification.Application.Interfaces;

namespace TruckDelivery.Notification.Infrastructure.Notifications;

// TODO: Replace with real Twilio implementation when SMS credentials are available.
public sealed class StubSmsSender(ILogger<StubSmsSender> logger) : ISmsNotificationSender
{
    public Task SendAsync(string phone, string body, CancellationToken ct = default)
    {
        logger.LogInformation("[SMS-STUB] To={Phone} Body={Body}", phone, body);
        return Task.CompletedTask;
    }
}
