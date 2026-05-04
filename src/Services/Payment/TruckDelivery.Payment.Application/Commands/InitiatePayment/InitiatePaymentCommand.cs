using MediatR;
using TruckDelivery.Payment.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Payment.Application.Commands.InitiatePayment;

public sealed record InitiatePaymentCommand(
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    PaymentMethod Method,
    string ClientIpAddress,
    string Currency = "VND") : IRequest<Result<InitiatePaymentResult>>;

public sealed record InitiatePaymentResult(Guid PaymentId, string? PaymentUrl);
