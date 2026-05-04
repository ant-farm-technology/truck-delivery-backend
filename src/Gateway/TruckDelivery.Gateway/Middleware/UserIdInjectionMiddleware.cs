using System.Security.Claims;

namespace TruckDelivery.Gateway.Middleware;

// Runs after UseAuthentication() — injects X-User-Id header from JWT sub claim.
// AspNetCoreRateLimit then uses this header as the client key for per-user rate limiting.
public sealed class UserIdInjectionMiddleware(RequestDelegate next)
{
    private const string UserIdHeader = "X-User-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
            context.Request.Headers[UserIdHeader] = userId;

        await next(context);
    }
}
