using TruckDelivery.Notification.Domain.Aggregates;
using TruckDelivery.Notification.Domain.Repositories;

namespace TruckDelivery.Notification.Infrastructure.Persistence.EFCore.Repositories;

public sealed class NotificationRepository(NotificationDbContext context) : INotificationRepository
{
    public async Task AddAsync(NotificationRecord notification, CancellationToken ct = default)
        => await context.Notifications.AddAsync(notification, ct);

    public Task UpdateAsync(NotificationRecord notification, CancellationToken ct = default)
    {
        context.Notifications.Update(notification);
        return Task.CompletedTask;
    }
}
