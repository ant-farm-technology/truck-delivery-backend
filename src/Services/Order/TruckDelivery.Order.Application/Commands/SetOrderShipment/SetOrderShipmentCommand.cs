using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Order.Application.Commands.SetOrderShipment;

public sealed record SetOrderShipmentCommand(Guid OrderId, Guid ShipmentId) : IRequest<Result>;
