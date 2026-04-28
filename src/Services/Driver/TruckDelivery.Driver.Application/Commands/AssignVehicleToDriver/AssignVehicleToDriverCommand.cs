using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.AssignVehicleToDriver;

public sealed record AssignVehicleToDriverCommand(Guid VehicleId, Guid DriverId) : IRequest<Result>;
