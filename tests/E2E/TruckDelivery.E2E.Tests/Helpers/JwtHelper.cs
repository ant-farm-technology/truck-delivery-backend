using Xunit;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TruckDelivery.E2E.Tests.Helpers;

/// <summary>
/// Generates JWT tokens for E2E tests, bypassing the Identity service.
/// Uses the same key/issuer/audience configured in test fixture.
/// </summary>
public static class JwtHelper
{
    public const string TestSecretKey = "TruckDelivery-E2E-Super-Secret-Key-For-Tests-Only!";
    public const string TestIssuer = "TruckDelivery";
    public const string TestAudience = "TruckDelivery";

    public static string GenerateToken(Guid userId, string email, string role, string? firstName = null, string? lastName = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
        };

        if (firstName is not null) claims.Add(new(ClaimTypes.GivenName, firstName));
        if (lastName is not null) claims.Add(new(ClaimTypes.Surname, lastName));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string AdminToken(Guid userId, string email = "admin@test.com")
        => GenerateToken(userId, email, "Admin", "Admin", "Test");

    public static string CustomerToken(Guid userId, string email = "customer@test.com")
        => GenerateToken(userId, email, "Customer", "Customer", "Test");

    public static string DriverToken(Guid userId, string email = "driver@test.com")
        => GenerateToken(userId, email, "Driver", "Driver", "Test");
}
