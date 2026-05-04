using MediatR;
using TruckDelivery.Payment.Application.Interfaces;
using TruckDelivery.Payment.Domain.Repositories;
using TruckDelivery.Payment.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Payment.Application.Commands.InitiatePayment;

public sealed class InitiatePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    IPaymentGatewayFactory gatewayFactory) : IRequestHandler<InitiatePaymentCommand, Result<InitiatePaymentResult>>
{
    public async Task<Result<InitiatePaymentResult>> Handle(InitiatePaymentCommand request, CancellationToken ct)
    {
        var existing = await paymentRepository.GetByOrderIdAsync(request.OrderId, ct);
        if (existing is not null)
            return Result.Failure<InitiatePaymentResult>(
                Error.Conflict("Payment", $"Payment for order {request.OrderId} already exists."));

        var payment = Domain.Aggregates.Payment.Create(
            request.OrderId, request.CustomerId, request.Amount,
            request.Method, request.Currency);

        payment.MarkPending();
        await paymentRepository.AddAsync(payment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var gateway = gatewayFactory.GetGateway(request.Method);
        var url = await gateway.CreatePaymentUrlAsync(
            payment.Id, payment.Amount, payment.Currency,
            $"Thanh toan don hang {request.OrderId}",
            request.ClientIpAddress, ct);

        if (request.Method == PaymentMethod.Cod)
        {
            // COD auto-completes via OrderDeliveredConsumer — no further action needed here
        }

        return Result.Success(new InitiatePaymentResult(payment.Id, url));
    }
}
