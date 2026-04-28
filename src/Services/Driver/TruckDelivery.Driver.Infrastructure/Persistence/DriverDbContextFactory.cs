using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TruckDelivery.Driver.Infrastructure.Persistence;

public sealed class DriverDbContextFactory : IDesignTimeDbContextFactory<DriverDbContext>
{
    public DriverDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DriverDbContext>()
            .UseMySql(
                "Server=localhost;Port=3306;Database=truck_driver;User=root;Password=root;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;

        return new DriverDbContext(options);
    }
}
