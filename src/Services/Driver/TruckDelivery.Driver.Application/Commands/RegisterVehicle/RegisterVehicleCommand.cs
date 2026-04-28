using MediatR;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.RegisterVehicle;

public sealed record RegisterVehicleCommand(
    string LicensePlate,
    string Brand,
    string Model,
    VehicleType Type,
    decimal MaxWeightKg,
    decimal MaxVolumeCbm,
    int YearOfManufacture) : IRequest<Result<Guid>>;
