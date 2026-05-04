using FluentAssertions;
using TruckDelivery.Analytics.Domain.Documents;

namespace TruckDelivery.Analytics.Domain.Tests.Documents;

public sealed class FraudAlertTests
{
    private static readonly Guid OriginalDriverId = Guid.NewGuid();
    private static readonly Guid ReplacementDriverId = Guid.NewGuid();

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_Should_SetDriverIds()
    {
        var alert = FraudAlert.Create(OriginalDriverId, ReplacementDriverId, 4, DateTime.UtcNow);

        alert.OriginalDriverId.Should().Be(OriginalDriverId);
        alert.ReplacementDriverId.Should().Be(ReplacementDriverId);
    }

    [Fact]
    public void Create_Should_SetSwapCount()
    {
        var alert = FraudAlert.Create(OriginalDriverId, ReplacementDriverId, 7, DateTime.UtcNow);

        alert.SwapCount.Should().Be(7);
    }

    [Fact]
    public void Create_Should_SetDetectedAt()
    {
        var detectedAt = DateTime.UtcNow.AddHours(-1);
        var alert = FraudAlert.Create(OriginalDriverId, ReplacementDriverId, 4, detectedAt);

        alert.DetectedAt.Should().Be(detectedAt);
    }

    [Fact]
    public void Create_Should_SetUnacknowledgedByDefault()
    {
        var alert = FraudAlert.Create(OriginalDriverId, ReplacementDriverId, 4, DateTime.UtcNow);

        alert.IsAcknowledged.Should().BeFalse();
    }

    [Fact]
    public void Create_Should_GenerateUniqueIds()
    {
        var a1 = FraudAlert.Create(OriginalDriverId, ReplacementDriverId, 4, DateTime.UtcNow);
        var a2 = FraudAlert.Create(OriginalDriverId, ReplacementDriverId, 4, DateTime.UtcNow);

        a1.Id.Should().NotBe(a2.Id);
    }

    // ── Threshold: collusion detected at > 3 swaps ────────────────────────────

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(10)]
    public void Create_Should_AcceptSwapCountAboveThreshold(int swapCount)
    {
        // FraudPatternAnalyzerJob triggers when swap count > 3
        var alert = FraudAlert.Create(OriginalDriverId, ReplacementDriverId, swapCount, DateTime.UtcNow);

        alert.SwapCount.Should().Be(swapCount);
        alert.SwapCount.Should().BeGreaterThan(3);
    }

    // ── Driver symmetry: original ≠ replacement ───────────────────────────────

    [Fact]
    public void Create_Should_StoreBothDriverIdsSeparately()
    {
        var alert = FraudAlert.Create(OriginalDriverId, ReplacementDriverId, 4, DateTime.UtcNow);

        alert.OriginalDriverId.Should().NotBe(alert.ReplacementDriverId);
    }

    // ── DetectedAt in the past is valid (FraudPatternAnalyzerJob runs hourly) ──

    [Fact]
    public void Create_Should_AcceptPastDetectionTime()
    {
        var pastTime = DateTime.UtcNow.AddHours(-2);
        var alert = FraudAlert.Create(OriginalDriverId, ReplacementDriverId, 5, pastTime);

        alert.DetectedAt.Should().Be(pastTime);
    }
}
