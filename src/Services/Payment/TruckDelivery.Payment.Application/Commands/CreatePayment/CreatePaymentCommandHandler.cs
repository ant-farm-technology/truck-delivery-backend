using System.Text.Json;
using MediatR;
using TruckDelivery.Payment.Application.IntegrationEvents;
using TruckDelivery.Payment.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Payment.Application.Commands.CreatePayment;

public sealed class CreatePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    IOutboxRepository outboxRepository) : IRequestHandler<CreatePaymentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePaymentCommand request, CancellationToken ct)
    {
        var existing = await paymentRepository.GetByOrderIdAsync(request.OrderId, ct);
        if (existing is not null)
            return Result.Failure<Guid>(Error.Conflict("Payment", $"Payment for order {request.OrderId} already exists."));

        var payment = Domain.Aggregates.Payment.Create(request.OrderId, request.CustomerId, request.Amount, request.Currency);
        payment.MarkPending();
        // Simulate immediate completion for COD flow — real impl would call payment gateway
        payment.Authorize();
        payment.Complete();

        await paymentRepository.AddAsync(payment, ct);

        var completedEvent = new PaymentCompletedEvent(payment.Id, payment.OrderId, payment.CustomerId, payment.Amount, payment.Currency);
        await outboxRepository.AddAsync(OutboxMessage.Create(
            nameof(PaymentCompletedEvent),
            "payment.payment.completed",
            payment.OrderId.ToString(),
            JsonSerializer.Serialize(completedEvent)), ct);

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(payment.Id);
    }
}
