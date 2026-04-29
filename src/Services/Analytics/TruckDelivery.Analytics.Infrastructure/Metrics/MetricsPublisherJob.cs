using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using TruckDelivery.Analytics.Domain.Repositories;

namespace TruckDelivery.Analytics.Infrastructure.Metrics;

// Publishes KPI gauges to Prometheus every minute by querying MongoDB.
public sealed class MetricsPublisherJob(
    IServiceScopeFactory scopeFactory,
    ILogger<MetricsPublisherJob> logger)
    : BackgroundService
{
    private static readonly Gauge ReassignmentSuccessRate = Prometheus.Metrics.CreateGauge(
        "analytics_reassignment_success_rate_pct",
        "Breakdown reassignment success rate (%) over last 30 days");

    private static readonly Gauge AvgRecoveryTimeMinutes = Prometheus.Metrics.CreateGauge(
        "analytics_avg_recovery_time_minutes",
        "Average breakdown recovery time in minutes over last 30 days");

    private static readonly Counter BreakdownsTotal = Prometheus.Metrics.CreateCounter(
        "analytics_breakdown_incidents_total",
        "Total breakdown incidents recorded",
        new CounterConfiguration { LabelNames = ["risk_level"] });

    private static readonly Counter FraudAlertsTotal = Prometheus.Metrics.CreateCounter(
        "analytics_fraud_alerts_total",
        "Total fraud alerts (suspicious driver pairs) recorded");

    private static long _lastBreakdownCount;
    private static long _lastFraudAlertCount;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MetricsPublisherJob failed — will retry next cycle");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task PublishAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var incidentRepo = scope.ServiceProvider.GetRequiredService<IBreakdownIncidentRepository>();
        var fraudRepo = scope.ServiceProvider.GetRequiredService<IFraudAlertRepository>();

        var from30d = DateTime.UtcNow.AddDays(-30);

        var total = await incidentRepo.CountAsync(from30d, ct);
        var successful = await incidentRepo.CountSuccessfullyReassignedAsync(from30d, ct);
        var avgRecovery = await incidentRepo.AverageRecoveryTimeMinutesAsync(from30d, ct);
        var fraudCount = await fraudRepo.CountAsync(from30d, ct);
        var byRiskLevel = await incidentRepo.CountByRiskLevelAsync(from30d, ct);

        ReassignmentSuccessRate.Set(total == 0 ? 0 : Math.Round((double)successful / total * 100, 1));
        AvgRecoveryTimeMinutes.Set(avgRecovery ?? 0);

        // Drive counters by delta (counters can only increment)
        var newBreakdowns = total - _lastBreakdownCount;
        if (newBreakdowns > 0)
        {
            foreach (var (riskLevel, _) in byRiskLevel)
                BreakdownsTotal.WithLabels(riskLevel).Inc(0); // ensure label exists
        }

        var newFraudAlerts = fraudCount - _lastFraudAlertCount;
        if (newFraudAlerts > 0) FraudAlertsTotal.Inc(newFraudAlerts);

        _lastBreakdownCount = total;
        _lastFraudAlertCount = fraudCount;

        logger.LogDebug("Metrics published: Total={Total} Successful={Successful} AvgRecovery={AvgRecovery}min Fraud={Fraud}",
            total, successful, avgRecovery, fraudCount);
    }
}
