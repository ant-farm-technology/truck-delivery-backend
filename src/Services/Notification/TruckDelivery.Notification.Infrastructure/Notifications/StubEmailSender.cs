using Microsoft.Extensions.Logging;
using TruckDelivery.Notification.Application.Interfaces;

namespace TruckDelivery.Notification.Infrastructure.Notifications;

public sealed class StubEmailSender(ILogger<StubEmailSender> logger) : IEmailNotificationSender
{
    public Task SendAsync(string email, string subject, string body, CancellationToken ct = default)
    {
        logger.LogInformation("[EMAIL-STUB] To={Email} Subject={Subject}", email, subject);
        return Task.CompletedTask;
    }
}
