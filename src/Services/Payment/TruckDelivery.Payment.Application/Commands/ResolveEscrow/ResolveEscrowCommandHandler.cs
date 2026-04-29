using MediatR;
using TruckDelivery.Payment.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Payment.Application.Commands.ResolveEscrow;

public sealed class ResolveEscrowCommandHandler(
    IEscrowPaymentRepository escrowRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ResolveEscrowCommand, Result>
{
    public async Task<Result> Handle(ResolveEscrowCommand request, CancellationToken ct)
    {
        var escrow = await escrowRepository.GetByIdAsync(request.EscrowId, ct);
        if (escrow is null)
            return Result.Failure(Error.NotFound("Escrow", $"Escrow {request.EscrowId} not found."));

        if (request.Resolution == EscrowResolution.Confirm)
            escrow.Release(request.Note);
        else
            escrow.Dispute(request.Note ?? "Customer disputed delivery");

        await escrowRepository.UpdateAsync(escrow, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
