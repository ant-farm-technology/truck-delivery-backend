using MediatR;
using TruckDelivery.Payment.Application.DTOs;

namespace TruckDelivery.Payment.Application.Queries.GetEscrowByOrder;

public sealed record GetEscrowByOrderQuery(Guid OrderId) : IRequest<EscrowDto?>;
