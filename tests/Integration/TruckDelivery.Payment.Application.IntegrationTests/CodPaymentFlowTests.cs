using Xunit;
using FluentAssertions;
using TruckDelivery.Payment.Application.Commands.CreatePayment;
using TruckDelivery.Payment.Application.IntegrationTests.Fixtures;
using TruckDelivery.Payment.Domain.Aggregates;

namespace TruckDelivery.Payment.Application.IntegrationTests;

[Collection("PaymentIntegration")]
public sealed class CodPaymentFlowTests(PaymentTestFixture fixture)
{
    private static CreatePaymentCommand BuildCommand(Guid? orderId = null) => new(
        OrderId: orderId ?? Guid.NewGuid(),
        CustomerId: Guid.NewGuid(),
        Amount: 120_000m,
        Currency: "VND");

    [Fact]
    public async Task Handle_Should_PersistPayment_WithCompletedStatus_ForCodFlow()
    {
        var handler = new CreatePaymentCommandHandler(
            fixture.PaymentRepository,
            fixture.UnitOfWork,
            fixture.OutboxRepository);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var payment = await fixture.PaymentRepository.GetByIdAsync(result.Value);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task Handle_Should_CreateOutboxEntry_ForPaymentCompletedEvent()
    {
        var handler = new CreatePaymentCommandHandler(
            fixture.PaymentRepository,
            fixture.UnitOfWork,
            fixture.OutboxRepository);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        var unprocessed = await fixture.OutboxRepository.GetUnprocessedAsync(20);
        unprocessed.Should().Contain(m => m.EventType.Contains("PaymentCompleted"));
    }

    [Fact]
    public async Task Handle_Should_Fail_WithConflict_WhenPaymentAlreadyExistsForOrder()
    {
        var orderId = Guid.NewGuid();
        var handler = new CreatePaymentCommandHandler(
            fixture.PaymentRepository,
            fixture.UnitOfWork,
            fixture.OutboxRepository);

        var first = await handler.Handle(BuildCommand(orderId), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.Handle(BuildCommand(orderId), CancellationToken.None);
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Payment");
    }

    [Fact]
    public async Task Handle_Should_SetCorrectAmount_AndCurrency()
    {
        var orderId = Guid.NewGuid();
        var handler = new CreatePaymentCommandHandler(
            fixture.PaymentRepository,
            fixture.UnitOfWork,
            fixture.OutboxRepository);

        var cmd = new CreatePaymentCommand(orderId, Guid.NewGuid(), 250_000m, "VND");
        var result = await handler.Handle(cmd, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        var payment = await fixture.PaymentRepository.GetByOrderIdAsync(orderId);
        payment!.Amount.Should().Be(250_000m);
        payment.Currency.Should().Be("VND");
    }
}
