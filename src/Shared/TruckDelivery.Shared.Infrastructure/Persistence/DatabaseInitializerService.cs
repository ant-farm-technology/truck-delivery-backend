using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TruckDelivery.Shared.Infrastructure.Persistence;

public sealed class DatabaseInitializerService<TContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseInitializerService<TContext>> logger) : IHostedService
    where TContext : DbContext
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migrations applied for {Context}", typeof(TContext).Name);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
