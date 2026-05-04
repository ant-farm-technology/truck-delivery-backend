namespace TruckDelivery.Notification.Application.Interfaces;

public interface IPushNotificationSender
{
    Task SendAsync(Guid recipientId, string title, string body, CancellationToken ct = default);
}
