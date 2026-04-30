using Microsoft.EntityFrameworkCore;
using TruckDelivery.Notification.Application.Commands.RegisterDevice;
using TruckDelivery.Notification.Domain.Aggregates;

namespace TruckDelivery.Notification.Infrastructure.Persistence.EFCore;

public sealed class DeviceTokenStore(NotificationDbContext db) : IDeviceTokenStore
{
    public async Task UpsertAsync(Guid userId, string token, string platform, CancellationToken ct = default)
    {
        var existing = await db.DeviceTokens
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Platform == platform.ToLowerInvariant(), ct);

        if (existing is not null)
        {
            db.DeviceTokens.Remove(existing);
        }

        db.DeviceTokens.Add(DeviceToken.Create(userId, token, platform));
        await db.SaveChangesAsync(ct);
    }
}
