using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Shipment.Application.Commands.ConfirmDispatch;

public sealed record ConfirmDispatchCommand(Guid ShipmentId) : IRequest<Result>;
