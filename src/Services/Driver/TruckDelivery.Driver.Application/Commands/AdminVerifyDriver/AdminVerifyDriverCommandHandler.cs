using MediatR;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.AdminVerifyDriver;

public sealed class AdminVerifyDriverCommandHandler(
    IDriverRepository driverRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AdminVerifyDriverCommand, Result>
{
    public async Task<Result> Handle(AdminVerifyDriverCommand request, CancellationToken ct)
    {
        var driver = await driverRepository.GetByIdAsync(request.DriverId, ct);
        if (driver is null)
            return Result.Failure(Error.NotFound("Driver", request.DriverId));

        var result = driver.AdminVerify(request.Notes);
        if (result.IsFailure)
            return result;

        driverRepository.Update(driver);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
