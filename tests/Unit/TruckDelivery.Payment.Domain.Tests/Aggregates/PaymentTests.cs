using Xunit;
using FluentAssertions;
using TruckDelivery.Payment.Domain.Aggregates;
using TruckDelivery.Payment.Domain.Exceptions;
using PaymentAggregate = TruckDelivery.Payment.Domain.Aggregates.Payment;

namespace TruckDelivery.Payment.Domain.Tests.Aggregates;

public sealed class PaymentTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_Should_SetStatusCreated()
    {
        var payment = PaymentAggregate.Create(OrderId, CustomerId, 150_000m);

        payment.Status.Should().Be(PaymentStatus.Created);
        payment.Amount.Should().Be(150_000m);
        payment.Currency.Should().Be("VND");
        payment.OrderId.Should().Be(OrderId);
    }

    [Fact]
    public void Create_Should_Throw_WhenAmountZero()
    {
        var act = () => PaymentAggregate.Create(OrderId, CustomerId, 0m);

        act.Should().Throw<PaymentDomainException>()
           .WithMessage("*positive*");
    }

    [Fact]
    public void Create_Should_Throw_WhenAmountNegative()
    {
        var act = () => PaymentAggregate.Create(OrderId, CustomerId, -1m);

        act.Should().Throw<PaymentDomainException>();
    }

    [Fact]
    public void Create_Should_UseDefaultCurrency_VND()
    {
        var payment = PaymentAggregate.Create(OrderId, CustomerId, 100_000m);

        payment.Currency.Should().Be("VND");
    }

    [Fact]
    public void Create_Should_RaisePaymentCreatedEvent()
    {
        var payment = PaymentAggregate.Create(OrderId, CustomerId, 100_000m);

        payment.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "PaymentCreatedDomainEvent");
    }

    // ── State machine: MarkPending ────────────────────────────────────────────

    [Fact]
    public void MarkPending_Should_Succeed_WhenCreated()
    {
        var payment = PaymentAggregate.Create(OrderId, CustomerId, 100_000m);

        payment.MarkPending();

        payment.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public void MarkPending_Should_Throw_WhenNotCreated()
    {
        var payment = PaymentAggregate.Create(OrderId, CustomerId, 100_000m);
        payment.MarkPending(); // now Pending

        var act = () => payment.MarkPending();

        act.Should().Throw<PaymentDomainException>();
    }

    // ── Authorize ─────────────────────────────────────────────────────────────

    [Fact]
    public void Authorize_Should_Succeed_WhenPending()
    {
        var payment = CreatePendingPayment();

        payment.Authorize();

        payment.Status.Should().Be(PaymentStatus.Authorized);
    }

    [Fact]
    public void Authorize_Should_Throw_WhenNotPending()
    {
        var payment = PaymentAggregate.Create(OrderId, CustomerId, 100_000m);

        var act = () => payment.Authorize();

        act.Should().Throw<PaymentDomainException>();
    }

    // ── Complete ──────────────────────────────────────────────────────────────

    [Fact]
    public void Complete_Should_Succeed_WhenAuthorized()
    {
        var payment = CreateAuthorizedPayment();

        payment.Complete();

        payment.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public void Complete_Should_Throw_WhenCreated()
    {
        var payment = PaymentAggregate.Create(OrderId, CustomerId, 100_000m);

        var act = () => payment.Complete();

        act.Should().Throw<PaymentDomainException>();
    }

    [Fact]
    public void Complete_Should_RaisePaymentCompletedEvent()
    {
        var payment = CreateAuthorizedPayment();
        payment.ClearDomainEvents();

        payment.Complete();

        payment.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "PaymentCompletedDomainEvent");
    }

    // ── Fail ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Fail_Should_SetFailedStatus()
    {
        var payment = CreatePendingPayment();

        payment.Fail("Card declined");

        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be("Card declined");
    }

    [Fact]
    public void Fail_Should_RaisePaymentFailedEvent()
    {
        var payment = CreatePendingPayment();
        payment.ClearDomainEvents();

        payment.Fail("timeout");

        payment.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "PaymentFailedDomainEvent");
    }

    // ── Refund ────────────────────────────────────────────────────────────────

    [Fact]
    public void Refund_Should_Succeed_WhenCompleted()
    {
        var payment = CreateCompletedPayment();

        payment.Refund();

        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void Refund_Should_Throw_WhenNotCompleted()
    {
        var payment = CreatePendingPayment();

        var act = () => payment.Refund();

        act.Should().Throw<PaymentDomainException>()
           .WithMessage("*completed*");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PaymentAggregate CreatePendingPayment()
    {
        var p = PaymentAggregate.Create(OrderId, CustomerId, 100_000m);
        p.MarkPending();
        return p;
    }

    private static PaymentAggregate CreateAuthorizedPayment()
    {
        var p = CreatePendingPayment();
        p.Authorize();
        return p;
    }

    private static PaymentAggregate CreateCompletedPayment()
    {
        var p = CreateAuthorizedPayment();
        p.Complete();
        return p;
    }
}
