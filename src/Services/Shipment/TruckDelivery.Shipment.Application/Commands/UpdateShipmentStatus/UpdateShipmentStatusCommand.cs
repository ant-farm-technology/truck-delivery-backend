using MediatR;
using TruckDelivery.Shipment.Domain.Aggregates;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Shipment.Application.Commands.UpdateShipmentStatus;

public sealed record UpdateShipmentStatusCommand(Guid ShipmentId, ShipmentStatus NewStatus) : IRequest<Result>;
