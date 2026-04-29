using MediatR;
using TruckDelivery.Payment.Domain.Aggregates;
using TruckDelivery.Payment.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Payment.Application.Commands.CreateEscrow;

public sealed class CreateEscrowCommandHandler(
    IEscrowPaymentRepository escrowRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateEscrowCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateEscrowCommand request, CancellationToken ct)
    {
        var existing = await escrowRepository.GetByShipmentIdAsync(request.ShipmentId, ct);
        if (existing is not null)
            return Result.Success(existing.Id);

        var escrow = EscrowPayment.Create(
            request.ShipmentId,
            request.OrderId,
            request.OriginalDriverId,
            request.ReplacementDriverId,
            request.LockedAmount,
            request.Currency);

        await escrowRepository.AddAsync(escrow, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(escrow.Id);
    }
}
