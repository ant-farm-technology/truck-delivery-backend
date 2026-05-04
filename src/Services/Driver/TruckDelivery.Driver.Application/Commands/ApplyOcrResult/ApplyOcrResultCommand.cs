using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.ApplyOcrResult;

public sealed record ApplyOcrResultCommand(
    Guid DriverId,
    string VerificationStatus,
    float ConfidenceScore,
    string? Notes = null) : IRequest<Result>;
