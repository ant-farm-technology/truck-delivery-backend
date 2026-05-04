using System.Text.Json;
using MediatR;
using TruckDelivery.Driver.Application.IntegrationEvents;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Driver.Application.Commands.UpdateDriverStatus;

public sealed class UpdateDriverStatusCommandHandler(
    IDriverRepository driverRepository,
    IUnitOfWork unitOfWork,
    IOutboxRepository outboxRepository)
    : IRequestHandler<UpdateDriverStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateDriverStatusCommand request, CancellationToken ct)
    {
        // Driver role can only update their own status
        if (request.RequestingUserRole == "Driver" && request.RequestingUserId != request.DriverId)
            return Result.Failure(Error.Forbidden("Driver.Authorization", "Drivers can only update their own status."));

        var driver = await driverRepository.GetByIdAsync(request.DriverId, ct);
        if (driver is null)
            return Result.Failure(Error.NotFound("Driver", request.DriverId));

        var oldStatus = driver.Status;
        var updateResult = driver.UpdateStatus(request.NewStatus);
        if (updateResult.IsFailure)
            return updateResult;

        driverRepository.Update(driver);

        var @event = new DriverStatusChangedEvent(
            driver.Id,
            oldStatus.ToString(),
            request.NewStatus.ToString(),
            driver.CurrentVehicleId);

        await outboxRepository.AddAsync(OutboxMessage.Create(
            eventType: nameof(DriverStatusChangedEvent),
            topic: "driver.driver.status-updated",
            partitionKey: driver.Id.ToString(),
            payload: JsonSerializer.Serialize(@event)), ct);

        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
