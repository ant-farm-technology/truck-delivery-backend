namespace TruckDelivery.Analytics.Application.DTOs;

public sealed record KpiSnapshotDto(
    int PeriodDays,
    long BreakdownCount,
    long SuccessfulReassignmentCount,
    double ReassignmentSuccessRatePct,
    double? AvgRecoveryTimeMinutes,
    long FraudAlertCount,
    IReadOnlyDictionary<string, long> BreakdownsByRiskLevel);
