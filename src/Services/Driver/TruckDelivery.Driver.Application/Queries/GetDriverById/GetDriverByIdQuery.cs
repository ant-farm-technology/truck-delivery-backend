using MediatR;
using TruckDelivery.Driver.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Queries.GetDriverById;

public sealed record GetDriverByIdQuery(Guid DriverId) : IRequest<Result<DriverDto>>;
