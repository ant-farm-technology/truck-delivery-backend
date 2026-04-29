using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Payment.Application.Commands.CreateEscrow;

public sealed record CreateEscrowCommand(
    Guid ShipmentId,
    Guid OrderId,
    Guid OriginalDriverId,
    Guid ReplacementDriverId,
    decimal LockedAmount,
    string Currency = "VND") : IRequest<Result<Guid>>;
