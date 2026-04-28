using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TruckDelivery.Notification.Infrastructure.Persistence.EFCore;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NotificationDbContext>();
        optionsBuilder.UseMySql(
            "Server=localhost;Port=3306;Database=truck_notification;User=root;Password=root;",
            ServerVersion.AutoDetect("Server=localhost;Port=3306;Database=truck_notification;User=root;Password=root;"));
        return new NotificationDbContext(optionsBuilder.Options);
    }
}
