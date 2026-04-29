using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TruckDelivery.Driver.Application.IntegrationEvents;
using TruckDelivery.Driver.Infrastructure.Persistence;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Driver.Infrastructure.Jobs;

// Runs hourly; detects collusion patterns where the same replacement driver
// has taken over from the same original driver more than 3 times.
// When detected: penalises both drivers' trust scores and publishes an alert event.
public sealed class FraudPatternAnalyzerJob(
    IServiceScopeFactory scopeFactory,
    ILogger<FraudPatternAnalyzerJob> logger) : BackgroundService
{
    private const int CollusionThreshold = 3;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);
            try
            {
                await RunAnalysisAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FraudPatternAnalyzerJob analysis cycle failed");
            }
        }
    }

    private async Task RunAnalysisAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DriverDbContext>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        // Group swap records by (OriginalDriverId, ReplacementDriverId) pair
        var suspiciousPairs = await db.DriverSwapRecords
            .GroupBy(r => new { r.OriginalDriverId, r.ReplacementDriverId })
            .Where(g => g.Count() > CollusionThreshold)
            .Select(g => new
            {
                g.Key.OriginalDriverId,
                g.Key.ReplacementDriverId,
                SwapCount = g.Count()
            })
            .ToListAsync(ct);

        if (suspiciousPairs.Count == 0)
            return;

        if (logger.IsEnabled(LogLevel.Warning))
            logger.LogWarning("FraudPatternAnalyzer detected {Count} suspicious driver pairs", suspiciousPairs.Count);

        foreach (var pair in suspiciousPairs)
        {
            // Penalise both drivers
            var original = await db.Drivers.FindAsync([pair.OriginalDriverId], ct);
            var replacement = await db.Drivers.FindAsync([pair.ReplacementDriverId], ct);

            if (original is not null)
            {
                original.UpdateTrustScore(-10, "collusion_pattern_detected");
            }

            if (replacement is not null)
            {
                replacement.UpdateTrustScore(-10, "collusion_pattern_detected");
            }

            // Publish alert event via outbox
            var alertEvent = new SuspiciousDriverPairDetectedEvent
            {
                OriginalDriverId = pair.OriginalDriverId,
                ReplacementDriverId = pair.ReplacementDriverId,
                SwapCount = pair.SwapCount,
                DetectedAt = DateTime.UtcNow
            };

            await outboxRepo.AddAsync(OutboxMessage.Create(
                nameof(SuspiciousDriverPairDetectedEvent),
                "driver.fraud.suspicious-pair-detected",
                pair.OriginalDriverId.ToString(),
                JsonSerializer.Serialize(alertEvent)), ct);

            logger.LogWarning(
                "Suspicious pair flagged: Original={OriginalDriverId} Replacement={ReplacementDriverId} SwapCount={SwapCount}",
                pair.OriginalDriverId, pair.ReplacementDriverId, pair.SwapCount);
        }

        await db.SaveChangesAsync(ct);
    }
}
