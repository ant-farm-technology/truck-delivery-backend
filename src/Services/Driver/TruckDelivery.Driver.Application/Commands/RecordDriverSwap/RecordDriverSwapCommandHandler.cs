using MediatR;
using TruckDelivery.Driver.Domain.Aggregates;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.RecordDriverSwap;

public sealed class RecordDriverSwapCommandHandler(
    IDriverSwapRecordRepository swapRecordRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RecordDriverSwapCommand, Result>
{
    public async Task<Result> Handle(RecordDriverSwapCommand request, CancellationToken ct)
    {
        var record = DriverSwapRecord.Create(
            request.OriginalDriverId,
            request.ReplacementDriverId,
            request.ShipmentId);

        await swapRecordRepository.AddAsync(record, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
