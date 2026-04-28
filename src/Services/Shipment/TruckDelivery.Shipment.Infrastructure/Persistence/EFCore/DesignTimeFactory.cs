using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TruckDelivery.Shipment.Infrastructure.Persistence.EFCore;

public sealed class ShipmentDbContextFactory : IDesignTimeDbContextFactory<ShipmentDbContext>
{
    public ShipmentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ShipmentDbContext>()
            .UseMySql(
                "Server=localhost;Port=3306;Database=truck_shipment;User=root;Password=root;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        return new ShipmentDbContext(options);
    }
}
