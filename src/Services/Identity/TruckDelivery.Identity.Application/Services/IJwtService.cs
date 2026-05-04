using TruckDelivery.Identity.Domain.Aggregates;

namespace TruckDelivery.Identity.Application.Services;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}
