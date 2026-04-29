using TruckDelivery.Driver.Domain.ValueObjects;

namespace TruckDelivery.Driver.Application.Interfaces;

public sealed record FraudGateResult(bool Passed, FraudRiskLevel RiskLevel, string? FailureReason);

public interface IBreakdownFraudGate
{
    Task<FraudGateResult> ValidateAsync(
        Guid driverId,
        double latitude,
        double longitude,
        IReadOnlyList<string> photoUrls,
        int trustScore,
        CancellationToken ct = default);
}
