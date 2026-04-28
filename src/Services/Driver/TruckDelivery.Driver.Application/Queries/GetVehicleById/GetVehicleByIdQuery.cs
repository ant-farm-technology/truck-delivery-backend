using MediatR;
using TruckDelivery.Driver.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Queries.GetVehicleById;

public sealed record GetVehicleByIdQuery(Guid VehicleId) : IRequest<Result<VehicleDto>>;
