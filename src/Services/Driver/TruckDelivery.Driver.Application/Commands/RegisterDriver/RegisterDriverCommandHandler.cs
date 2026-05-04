using System.Text.Json;
using MediatR;
using TruckDelivery.Driver.Application.IntegrationEvents;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Driver.Application.Commands.RegisterDriver;

public sealed class RegisterDriverCommandHandler(
    IDriverRepository driverRepository,
    IUnitOfWork unitOfWork,
    IOutboxRepository outboxRepository)
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
            request.LicenseNumber,
            request.LicenseGrade,
            request.LicenseExpiryDate,
            request.DateOfBirth,
            request.Address,
            request.IdCardNumber);

        if (driverResult.IsFailure)
            return Result.Failure(driverResult.Error);

        var driver = driverResult.Value;
        await driverRepository.AddAsync(driver, ct);

        var @event = new DriverRegisteredEvent(
            driver.Id,
            driver.Email,
            $"{driver.FirstName} {driver.LastName}",
            driver.PhoneNumber);

        await outboxRepository.AddAsync(OutboxMessage.Create(
            eventType: nameof(DriverRegisteredEvent),
            topic: "driver.driver.registered",
            partitionKey: driver.Id.ToString(),
            payload: JsonSerializer.Serialize(@event)), ct);

        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
