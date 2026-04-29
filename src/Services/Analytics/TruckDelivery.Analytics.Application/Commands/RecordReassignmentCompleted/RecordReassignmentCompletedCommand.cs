using MediatR;

namespace TruckDelivery.Analytics.Application.Commands.RecordReassignmentCompleted;

public sealed record RecordReassignmentCompletedCommand(
    Guid ShipmentId,
    Guid OriginalDriverId,
    Guid ReplacementDriverId) : IRequest;
