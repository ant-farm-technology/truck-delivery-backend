using MediatR;

namespace TruckDelivery.Analytics.Application.Commands.RecordFraudAlert;

public sealed record RecordFraudAlertCommand(
    Guid OriginalDriverId,
    Guid ReplacementDriverId,
    int SwapCount,
    DateTime DetectedAt) : IRequest;
