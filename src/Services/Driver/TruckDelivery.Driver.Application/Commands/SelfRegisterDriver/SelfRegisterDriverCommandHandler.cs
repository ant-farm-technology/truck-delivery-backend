using System.Text.Json;
using MediatR;
using TruckDelivery.Driver.Application.IntegrationEvents;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Driver.Application.Commands.SelfRegisterDriver;

public sealed class SelfRegisterDriverCommandHandler(
    IDriverRepository driverRepository,
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork,
    IOutboxRepository outboxRepository)
    : IRequestHandler<SelfRegisterDriverCommand, Result<SelfRegisterDriverResult>>
{
    public async Task<Result<SelfRegisterDriverResult>> Handle(SelfRegisterDriverCommand request, CancellationToken ct)
    {
        if (await driverRepository.ExistsByIdAsync(request.UserId, ct))
            return Result.Failure<SelfRegisterDriverResult>(
                Error.Conflict("Driver", "Driver profile already exists for this user."));

        if (await vehicleRepository.ExistsByLicensePlateAsync(request.LicensePlate, ct))
            return Result.Failure<SelfRegisterDriverResult>(
                Error.Conflict("Vehicle", $"Vehicle with plate '{request.LicensePlate}' already registered."));

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
            return Result.Failure<SelfRegisterDriverResult>(driverResult.Error);

        var driver = driverResult.Value;

        var vehicleResult = Domain.Aggregates.Vehicle.Create(
            request.LicensePlate,
            request.Brand,
            request.Model,
            request.VehicleType,
            request.MaxWeightKg,
            request.MaxVolumeCbm,
            request.LengthM,
            request.WidthM,
            request.HeightM,
            request.YearOfManufacture,
            request.RegistrationNumber,
            request.RegistrationExpiryDate);

        if (vehicleResult.IsFailure)
            return Result.Failure<SelfRegisterDriverResult>(vehicleResult.Error);

        var vehicle = vehicleResult.Value;

        driver.SubmitDocuments(
            request.PortraitPhotoUrl,
            request.IdCardFrontUrl,
            request.IdCardBackUrl,
            request.LicenseFrontUrl,
            request.LicenseBackUrl,
            request.VehicleRegFrontUrl,
            request.VehicleRegBackUrl);

        await driverRepository.AddAsync(driver, ct);
        await vehicleRepository.AddAsync(vehicle, ct);

        var @event = new DriverDocumentsSubmittedEvent(
            driver.Id,
            vehicle.Id,
            request.PortraitPhotoUrl,
            request.IdCardFrontUrl,
            request.IdCardBackUrl,
            request.LicenseFrontUrl,
            request.LicenseBackUrl,
            request.VehicleRegFrontUrl,
            request.VehicleRegBackUrl);

        await outboxRepository.AddAsync(OutboxMessage.Create(
            eventType: nameof(DriverDocumentsSubmittedEvent),
            topic: "driver.documents.submitted",
            partitionKey: driver.Id.ToString(),
            payload: JsonSerializer.Serialize(@event)), ct);

        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new SelfRegisterDriverResult(
            driver.Id, vehicle.Id, driver.VerificationStatus.ToString()));
    }
}
