using Microsoft.EntityFrameworkCore;
using TruckDelivery.Driver.Domain.Aggregates;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Driver.Infrastructure.Persistence;

public sealed class DriverDbContext(DbContextOptions<DriverDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Aggregates.Driver> Drivers => Set<Domain.Aggregates.Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<BreakdownReport> BreakdownReports => Set<BreakdownReport>();
    public DbSet<DriverSwapRecord> DriverSwapRecords => Set<DriverSwapRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("driver");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DriverDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration("driver"));
    }
}
