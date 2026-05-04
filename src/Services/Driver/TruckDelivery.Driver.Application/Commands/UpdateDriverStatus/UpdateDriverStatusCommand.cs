using MediatR;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.UpdateDriverStatus;

public sealed record UpdateDriverStatusCommand(
    Guid DriverId,
    DriverStatus NewStatus,
    Guid RequestingUserId,
    string RequestingUserRole) : IRequest<Result>;
