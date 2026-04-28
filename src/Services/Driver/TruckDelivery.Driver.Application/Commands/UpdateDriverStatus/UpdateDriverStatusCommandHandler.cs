using MediatR;
using TruckDelivery.Driver.Application.IntegrationEvents;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Messaging;

namespace TruckDelivery.Driver.Application.Commands.UpdateDriverStatus;

public sealed class UpdateDriverStatusCommandHandler(
    IDriverRepository driverRepository,
    IUnitOfWork unitOfWork,
    IEventBus eventBus)
    : IRequestHandler<UpdateDriverStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateDriverStatusCommand request, CancellationToken ct)
    {
        var driver = await driverRepository.GetByIdAsync(request.DriverId, ct);
        if (driver is null)
            return Result.Failure(Error.NotFound("Driver", request.DriverId));

        var oldStatus = driver.Status;
        var updateResult = driver.UpdateStatus(request.NewStatus);
        if (updateResult.IsFailure)
            return updateResult;

        driverRepository.Update(driver);
        await unitOfWork.SaveChangesAsync(ct);

        await eventBus.PublishAsync(new DriverStatusChangedEvent(
            driver.Id,
            oldStatus.ToString(),
            request.NewStatus.ToString(),
            driver.CurrentVehicleId), ct);

        return Result.Success();
    }
}
