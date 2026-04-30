using MediatR;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.AdminRejectDriver;

public sealed class AdminRejectDriverCommandHandler(
    IDriverRepository driverRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AdminRejectDriverCommand, Result>
{
    public async Task<Result> Handle(AdminRejectDriverCommand request, CancellationToken ct)
    {
        var driver = await driverRepository.GetByIdAsync(request.DriverId, ct);
        if (driver is null)
            return Result.Failure(Error.NotFound("Driver", request.DriverId));

        var result = driver.AdminReject(request.Reason);
        if (result.IsFailure)
            return result;

        driverRepository.Update(driver);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
