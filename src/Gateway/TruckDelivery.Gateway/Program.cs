using System.Text.Json;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using TruckDelivery.Gateway.Middleware;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"];
        options.Audience = builder.Configuration["Jwt:Audience"];
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
    });

builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
});

builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// G-T6: Aggregate health check — polls /health on each downstream service
builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri("http://identity-service:8080/health"), name: "identity", tags: ["ready"])
    .AddUrlGroup(new Uri("http://order-service:8080/health"), name: "order", tags: ["ready"])
    .AddUrlGroup(new Uri("http://driver-service:8080/health"), name: "driver", tags: ["ready"])
    .AddUrlGroup(new Uri("http://shipment-service:8080/health"), name: "shipment", tags: ["ready"])
    .AddUrlGroup(new Uri("http://tracking-service:8080/health"), name: "tracking", tags: ["ready"])
    .AddUrlGroup(new Uri("http://notification-service:8080/health"), name: "notification", tags: ["ready"])
    .AddUrlGroup(new Uri("http://payment-service:8080/health"), name: "payment", tags: ["ready"])
    .AddUrlGroup(new Uri("http://analytics-service:8080/health"), name: "analytics", tags: ["ready"]);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "gateway",
        serviceNamespace: "truck-delivery"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(o =>
        {
            o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/metrics")
                           && !ctx.Request.Path.StartsWithSegments("/health")
                           && !ctx.Request.Path.StartsWithSegments("/ready");
        })
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(
            builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317")))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();

app.UseSerilogRequestLogging(o =>
{
    o.EnrichDiagnosticContext = (diag, ctx) =>
    {
        diag.Set("CorrelationId", ctx.Request.Headers["X-Correlation-Id"].ToString());
        diag.Set("ClientIp", ctx.Connection.RemoteIpAddress?.ToString());
    };
});

app.UseMiddleware<CorrelationIdMiddleware>();

// G-T5: Auth runs first so HttpContext.User is populated before injection middleware
app.UseAuthentication();

// G-T5: Inject X-User-Id from JWT sub — used by IpRateLimiting as ClientIdHeader
app.UseMiddleware<UserIdInjectionMiddleware>();

// Rate limiting runs after auth so per-user header is already set
app.UseIpRateLimiting();

app.UseAuthorization();

// Liveness — gateway itself only
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness — gateway + all downstream services
app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthJsonAsync
});

// G-T6: Aggregate health — returns detailed status per downstream service
app.MapHealthChecks("/health/all", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthJsonAsync
}).AllowAnonymous();

app.MapPrometheusScrapingEndpoint("/metrics");

app.MapReverseProxy();

await app.RunAsync();

static async Task WriteHealthJsonAsync(HttpContext ctx, HealthReport report)
{
    ctx.Response.ContentType = "application/json; charset=utf-8";

    var result = new
    {
        status = report.Status.ToString(),
        totalDuration = report.TotalDuration.TotalMilliseconds,
        services = report.Entries.ToDictionary(
            e => e.Key,
            e => new
            {
                status = e.Value.Status.ToString(),
                durationMs = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                error = e.Value.Exception?.Message
            })
    };

    await ctx.Response.WriteAsync(JsonSerializer.Serialize(result,
        new JsonSerializerOptions { WriteIndented = false }));
}
