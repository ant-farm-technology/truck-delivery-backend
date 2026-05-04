using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TruckDelivery.Identity.Infrastructure.Persistence.Seeds;

namespace TruckDelivery.Identity.Infrastructure.Persistence;

public sealed class DatabaseInitializerService(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseInitializerService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await AdminSeeder.SeedAsync(db);
        logger.LogInformation("Database migration and seeding completed");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
