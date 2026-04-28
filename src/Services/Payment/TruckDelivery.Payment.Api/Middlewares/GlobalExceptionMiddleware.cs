using TruckDelivery.Payment.Domain.Exceptions;

namespace TruckDelivery.Payment.Api.Middlewares;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (PaymentDomainException ex)
        {
            logger.LogWarning(ex, "Domain error");
            ctx.Response.StatusCode = 422;
            await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            ctx.Response.StatusCode = 500;
            await ctx.Response.WriteAsJsonAsync(new { error = "Internal server error" });
        }
    }
}
