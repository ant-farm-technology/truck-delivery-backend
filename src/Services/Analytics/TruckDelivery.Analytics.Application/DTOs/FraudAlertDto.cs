namespace TruckDelivery.Analytics.Application.DTOs;

public sealed record FraudAlertDto(
    Guid Id,
    Guid OriginalDriverId,
    Guid ReplacementDriverId,
    int SwapCount,
    DateTime DetectedAt,
    bool IsAcknowledged);
