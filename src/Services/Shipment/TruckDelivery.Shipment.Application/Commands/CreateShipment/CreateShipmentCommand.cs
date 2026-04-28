using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Shipment.Application.Commands.CreateShipment;

public sealed record CreateShipmentCommand(
    Guid OrderId,
    Guid CustomerId,
    string PickupCity,
    string PickupProvince,
    string DeliveryCity,
    string DeliveryProvince,
    decimal TotalWeightKg,
    decimal TotalVolumeCbm) : IRequest<Result<Guid>>;
