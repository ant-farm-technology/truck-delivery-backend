using MediatR;
using TruckDelivery.Driver.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Queries.GetMyDriverProfile;

public sealed record GetMyDriverProfileQuery(Guid UserId) : IRequest<Result<DriverDto>>;
