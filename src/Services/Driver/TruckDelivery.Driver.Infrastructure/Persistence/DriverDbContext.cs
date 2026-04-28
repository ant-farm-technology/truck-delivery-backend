using Microsoft.EntityFrameworkCore;
using TruckDelivery.Driver.Domain.Aggregates;

namespace TruckDelivery.Driver.Infrastructure.Persistence;

public sealed class DriverDbContext(DbContextOptions<DriverDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Aggregates.Driver> Drivers => Set<Domain.Aggregates.Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("driver");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DriverDbContext).Assembly);
    }
}
