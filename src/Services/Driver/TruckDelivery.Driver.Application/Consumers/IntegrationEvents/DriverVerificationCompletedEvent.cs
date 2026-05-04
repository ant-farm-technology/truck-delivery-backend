using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Driver.Application.Consumers.IntegrationEvents;

// Published by OCR service after async document verification
public sealed record DriverVerificationCompletedEvent(
    Guid DriverId,
    string VerificationStatus,   // "ocr_verified" | "manual_review" | "rejected"
    float OverallConfidenceScore,
    string? FailureReason = null) : IntegrationEvent;
