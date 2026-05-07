using Microsoft.EntityFrameworkCore;
using TruckDelivery.Identity.Domain.Aggregates;
using TruckDelivery.Identity.Domain.Repositories;
using TruckDelivery.Identity.Infrastructure.Persistence;

namespace TruckDelivery.Identity.Infrastructure.Repositories;

public sealed class UserRepository(IdentityDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return dbContext.Users.FirstOrDefaultAsync(u => u.Email.Equals(email), ct);
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        return dbContext.Users.AnyAsync(u => u.Email.Equals(email), ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await dbContext.Users.AddAsync(user, ct);
    }

    public void Update(User user)
    {
        dbContext.Users.Update(user);
    }
}
