using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Tracking.Application.Commands.StopTracking;

public sealed record StopTrackingCommand(Guid ShipmentId) : IRequest<Result>;
