using TruckDelivery.Notification.Domain.Aggregates;

namespace TruckDelivery.Notification.Domain.Repositories;

public interface INotificationRepository
{
    Task AddAsync(NotificationRecord notification, CancellationToken ct = default);
    Task UpdateAsync(NotificationRecord notification, CancellationToken ct = default);
}
