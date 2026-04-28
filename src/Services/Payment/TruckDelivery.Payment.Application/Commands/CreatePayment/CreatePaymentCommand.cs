using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Payment.Application.Commands.CreatePayment;

public sealed record CreatePaymentCommand(
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency = "VND") : IRequest<Result<Guid>>;
