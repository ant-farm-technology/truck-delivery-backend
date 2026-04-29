using Serilog.Context;

namespace TruckDelivery.Analytics.Api.Middlewares;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string Header = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext ctx)
    {
        var correlationId = ctx.Request.Headers[Header].FirstOrDefault()
            ?? Guid.NewGuid().ToString();
        ctx.Response.Headers[Header] = correlationId;
        using (LogContext.PushProperty("CorrelationId", correlationId))
            await next(ctx);
    }
}
