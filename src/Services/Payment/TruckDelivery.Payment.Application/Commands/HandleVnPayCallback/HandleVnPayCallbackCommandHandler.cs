using System.Text.Json;
using MediatR;
using TruckDelivery.Payment.Application.IntegrationEvents;
using TruckDelivery.Payment.Application.Interfaces;
using TruckDelivery.Payment.Domain.Repositories;
using TruckDelivery.Payment.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Payment.Application.Commands.HandleVnPayCallback;

public sealed class HandleVnPayCallbackCommandHandler(
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    IOutboxRepository outboxRepository,
    IPaymentGatewayFactory gatewayFactory) : IRequestHandler<HandleVnPayCallbackCommand, Result<HandleVnPayCallbackResult>>
{
    public async Task<Result<HandleVnPayCallbackResult>> Handle(HandleVnPayCallbackCommand request, CancellationToken ct)
    {
        var gateway = gatewayFactory.GetGateway(PaymentMethod.VnPay);
        var (isSuccess, txnRef, failureReason) = await gateway.VerifyCallbackAsync(request.QueryParams, ct);

        // vnp_TxnRef is the paymentId we set when creating the URL
        if (!request.QueryParams.TryGetValue("vnp_TxnRef", out var txnRefStr) || !Guid.TryParse(txnRefStr, out var paymentId))
            return Result.Failure<HandleVnPayCallbackResult>(Error.Validation("VnPay.Callback", "Missing or invalid vnp_TxnRef."));

        var payment = await paymentRepository.GetByIdAsync(paymentId, ct);
        if (payment is null)
            return Result.Failure<HandleVnPayCallbackResult>(Error.NotFound("Payment", paymentId.ToString()));

        if (isSuccess)
        {
            payment.Authorize();
            payment.Complete();

            var completedEvent = new PaymentCompletedEvent(payment.Id, payment.OrderId, payment.CustomerId, payment.Amount, payment.Currency);
            await outboxRepository.AddAsync(OutboxMessage.Create(
                nameof(PaymentCompletedEvent),
                "payment.payment.completed",
                payment.OrderId.ToString(),
                JsonSerializer.Serialize(completedEvent)), ct);
        }
        else
        {
            payment.Fail(failureReason ?? "VNPay payment failed.");

            var failedEvent = new PaymentFailedEvent(payment.Id, payment.OrderId, failureReason ?? "VNPay payment failed.");
            await outboxRepository.AddAsync(OutboxMessage.Create(
                nameof(PaymentFailedEvent),
                "payment.payment.failed",
                payment.OrderId.ToString(),
                JsonSerializer.Serialize(failedEvent)), ct);
        }

        await paymentRepository.UpdateAsync(payment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new HandleVnPayCallbackResult(isSuccess, paymentId, failureReason));
    }
}
