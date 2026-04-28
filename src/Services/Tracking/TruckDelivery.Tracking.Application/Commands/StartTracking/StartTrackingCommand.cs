using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Tracking.Application.Commands.StartTracking;

public sealed record StartTrackingCommand(
    Guid ShipmentId,
    Guid OrderId,
    Guid DriverId) : IRequest<Result<Guid>>;
