using Microsoft.EntityFrameworkCore;
using TruckDelivery.Identity.Domain.Aggregates;
using TruckDelivery.Identity.Domain.ValueObjects;
using TruckDelivery.Identity.Infrastructure.Persistence;

namespace TruckDelivery.Identity.Infrastructure.Persistence.Seeds;

public static class AdminSeeder
{
    public static async Task SeedAsync(IdentityDbContext context)
    {
        if (await context.Users.AnyAsync(u => u.Role == UserRole.Admin))
            return;

        var result = User.Create(
            email: "admin@truckdelivery.vn",
            password: "Admin@123456",
            firstName: "System",
            lastName: "Admin",
            role: UserRole.Admin,
            phoneNumber: "+84901000001");

        if (result.IsFailure)
            throw new InvalidOperationException($"AdminSeeder failed: {result.Error.Description}");

        await context.Users.AddAsync(result.Value);
        await context.SaveChangesAsync();
    }
}
