using FluentValidation;

namespace TruckDelivery.Identity.Api.Middlewares;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Validation failed");
            ctx.Response.StatusCode = 400;
            var errors = ex.Errors.Select(e => e.ErrorMessage);
            await ctx.Response.WriteAsJsonAsync(new { code = "VALIDATION_ERROR", errors });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            ctx.Response.StatusCode = 500;
            await ctx.Response.WriteAsJsonAsync(new { code = "SERVER_ERROR", message = "Internal server error" });
        }
    }
}
