using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Payment.Application.Commands.ResolveEscrow;

public enum EscrowResolution { Confirm, Dispute }

public sealed record ResolveEscrowCommand(
    Guid EscrowId,
    EscrowResolution Resolution,
    string? Note = null) : IRequest<Result>;
