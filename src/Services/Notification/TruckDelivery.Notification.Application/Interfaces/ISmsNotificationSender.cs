namespace TruckDelivery.Notification.Application.Interfaces;

public interface ISmsNotificationSender
{
    Task SendAsync(string phoneNumber, string body, CancellationToken ct = default);
}
