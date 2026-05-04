using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Shipment.Application.Commands.FailShipment;

public sealed record FailShipmentCommand(Guid ShipmentId, string Reason) : IRequest<Result>;
