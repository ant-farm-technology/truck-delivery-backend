using MediatR;
using TruckDelivery.Driver.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Queries.ListAvailableDrivers;

public sealed record ListAvailableDriversQuery : IRequest<Result<IReadOnlyList<DriverSummaryDto>>>;
