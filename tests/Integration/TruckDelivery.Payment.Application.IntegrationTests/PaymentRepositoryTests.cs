using FluentAssertions;
using TruckDelivery.Payment.Application.IntegrationTests.Fixtures;
using TruckDelivery.Payment.Domain.Aggregates;

namespace TruckDelivery.Payment.Application.IntegrationTests;

[Collection("PaymentIntegration")]
public sealed class PaymentRepositoryTests(PaymentTestFixture fixture)
{
    [Fact]
    public async Task Repository_Should_PersistPayment_AndRetrieveById()
    {
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var payment = Domain.Aggregates.Payment.Create(orderId, customerId, 150_000m, "VND");

        await fixture.PaymentRepository.AddAsync(payment);
        await fixture.UnitOfWork.SaveChangesAsync();

        var retrieved = await fixture.PaymentRepository.GetByIdAsync(payment.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(payment.Id);
        retrieved.OrderId.Should().Be(orderId);
        retrieved.CustomerId.Should().Be(customerId);
        retrieved.Amount.Should().Be(150_000m);
        retrieved.Currency.Should().Be("VND");
    }

    [Fact]
    public async Task Repository_Should_FindPayment_ByOrderId()
    {
        var orderId = Guid.NewGuid();
        var payment = Domain.Aggregates.Payment.Create(orderId, Guid.NewGuid(), 50_000m, "VND");
        await fixture.PaymentRepository.AddAsync(payment);
        await fixture.UnitOfWork.SaveChangesAsync();

        var retrieved = await fixture.PaymentRepository.GetByOrderIdAsync(orderId);

        retrieved.Should().NotBeNull();
        retrieved!.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task Repository_Should_ReturnNull_ForNonExistentPayment()
    {
        var result = await fixture.PaymentRepository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task Repository_Should_ReturnNull_ForNonExistentOrder()
    {
        var result = await fixture.PaymentRepository.GetByOrderIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task Repository_Should_PersistEscrow_AndRetrieveByShipmentId()
    {
        var shipmentId = Guid.NewGuid();
        var escrow = EscrowPayment.Create(
            shipmentId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            50_000m,
            "VND");

        await fixture.EscrowRepository.AddAsync(escrow);
        await fixture.UnitOfWork.SaveChangesAsync();

        var retrieved = await fixture.EscrowRepository.GetByShipmentIdAsync(shipmentId);

        retrieved.Should().NotBeNull();
        retrieved!.ShipmentId.Should().Be(shipmentId);
        retrieved.Status.Should().Be(EscrowStatus.Locked);
        retrieved.LockedAmount.Should().Be(50_000m);
    }
}
