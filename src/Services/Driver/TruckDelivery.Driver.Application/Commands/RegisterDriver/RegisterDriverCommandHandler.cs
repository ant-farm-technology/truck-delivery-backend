using MediatR;
using TruckDelivery.Driver.Application.IntegrationEvents;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Messaging;

namespace TruckDelivery.Driver.Application.Commands.RegisterDriver;

public sealed class RegisterDriverCommandHandler(
    IDriverRepository driverRepository,
    IUnitOfWork unitOfWork,
    IEventBus eventBus)
    : IRequestHandler<RegisterDriverCommand, Result>
{
    public async Task<Result> Handle(RegisterDriverCommand request, CancellationToken ct)
    {
        if (await driverRepository.ExistsByIdAsync(request.UserId, ct))
            return Result.Failure(Error.Conflict("Driver", "Driver profile already exists for this user."));

        var driverResult = Domain.Aggregates.Driver.Create(
            request.UserId,
            request.Email,
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            request.LicenseNumber);

        if (driverResult.IsFailure)
            return Result.Failure(driverResult.Error);

        var driver = driverResult.Value;
        await driverRepository.AddAsync(driver, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await eventBus.PublishAsync(new DriverRegisteredEvent(
            driver.Id,
            driver.Email,
            $"{driver.FirstName} {driver.LastName}",
            driver.PhoneNumber), ct);

        return Result.Success();
    }
}
