using MediatR;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.RegisterVehicle;

public sealed class RegisterVehicleCommandHandler(
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RegisterVehicleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterVehicleCommand request, CancellationToken ct)
    {
        if (await vehicleRepository.ExistsByLicensePlateAsync(request.LicensePlate, ct))
            return Result.Failure<Guid>(Error.Conflict("Vehicle.LicensePlate", "License plate already registered."));

        var vehicleResult = Domain.Aggregates.Vehicle.Create(
            request.LicensePlate,
            request.Brand,
            request.Model,
            request.Type,
            request.MaxWeightKg,
            request.MaxVolumeCbm,
            request.LengthM,
            request.WidthM,
            request.HeightM,
            request.YearOfManufacture,
            request.RegistrationNumber,
            request.RegistrationExpiryDate);

        if (vehicleResult.IsFailure)
            return Result.Failure<Guid>(vehicleResult.Error);

        await vehicleRepository.AddAsync(vehicleResult.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(vehicleResult.Value.Id);
    }
}
