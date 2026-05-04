using MediatR;
using TruckDelivery.Payment.Application.DTOs;

namespace TruckDelivery.Payment.Application.Queries.GetPaymentByOrder;

public sealed record GetPaymentByOrderQuery(Guid OrderId) : IRequest<PaymentDto?>;
