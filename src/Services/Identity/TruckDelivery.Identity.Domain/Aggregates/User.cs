using TruckDelivery.Shared.Common.Domain;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Identity.Domain.Events;
using TruckDelivery.Identity.Domain.ValueObjects;

namespace TruckDelivery.Identity.Domain.Aggregates;

public sealed class User : AggregateRoot<Guid>
{
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiresAt { get; private set; }

    private User() { }
    private User(Guid id) : base(id) { }

    public static Result<User> Create(string email, string password, string firstName, string lastName, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<User>(Error.Validation("User.Email", "Email is required"));
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return Result.Failure<User>(Error.Validation("User.Password", "Password must be at least 8 characters"));
        }

        var user = new User(Guid.NewGuid())
        {
            Email = email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        user.RaiseDomainEvent(new UserCreatedDomainEvent(user.Id, user.Email, user.Role));
        return Result.Success(user);
    }

    public bool VerifyPassword(string password) => BCrypt.Net.BCrypt.Verify(password, PasswordHash);

    public void SetRefreshToken(string refreshToken, DateTime expiresAt)
    {
        RefreshToken = refreshToken;
        RefreshTokenExpiresAt = expiresAt;
        LastLoginAt = DateTime.UtcNow;
    }

    public Result RevokeRefreshToken()
    {
        if (RefreshToken is null)
        {
            return Result.Failure(Error.Validation("User.RefreshToken", "No active refresh token"));
        }

        RefreshToken = null;
        RefreshTokenExpiresAt = null;
        return Result.Success();
    }

    public bool IsRefreshTokenValid(string token)
    {
        return RefreshToken == token && RefreshTokenExpiresAt > DateTime.UtcNow;
    }

    public void Deactivate() => IsActive = false;
}
