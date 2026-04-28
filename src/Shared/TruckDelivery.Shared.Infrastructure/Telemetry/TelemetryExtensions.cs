using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TruckDelivery.Shared.Infrastructure.Telemetry;

public static class TelemetryExtensions
{
    public static IServiceCollection AddTruckDeliveryTelemetry(this IServiceCollection services, string serviceName, string serviceVersion, string otlpEndpoint)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(serviceName)
                .AddSource("TruckDelivery.Kafka.Producer")
                .AddSource("TruckDelivery.Kafka.Consumer")
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter());

        return services;
    }
}
