using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.RecordDriverSwap;

public sealed record RecordDriverSwapCommand(
    Guid OriginalDriverId,
    Guid ReplacementDriverId,
    Guid ShipmentId) : IRequest<Result>;
