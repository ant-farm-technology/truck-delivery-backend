using MediatR;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.UpdateVehicleStatus;

public sealed record UpdateVehicleStatusCommand(Guid VehicleId, VehicleStatus Status) : IRequest<Result>;

public sealed class UpdateVehicleStatusCommandHandler(
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateVehicleStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateVehicleStatusCommand request, CancellationToken ct)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.VehicleId, ct);
        if (vehicle is null)
            return Result.Failure(Error.NotFound("Vehicle", request.VehicleId));

        var result = request.Status switch
        {
            VehicleStatus.Maintenance => vehicle.SetMaintenance(),
            VehicleStatus.Available when vehicle.AssignedDriverId is null => Result.Success(),
            _ => Result.Failure(Error.Validation("Vehicle.Status",
                $"Cannot set vehicle status to {request.Status} via this endpoint."))
        };

        if (result.IsFailure) return result;

        // For Available when not in Maintenance — allow direct set via admin
        if (request.Status == VehicleStatus.Available)
            vehicle.UnassignDriver();

        vehicleRepository.Update(vehicle);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
