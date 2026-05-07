using Xunit;
using FluentAssertions;
using TruckDelivery.Payment.Application.Commands.CreateEscrow;
using TruckDelivery.Payment.Application.Commands.ResolveEscrow;
using TruckDelivery.Payment.Application.IntegrationTests.Fixtures;
using TruckDelivery.Payment.Domain.Aggregates;
using static TruckDelivery.Payment.Application.Commands.ResolveEscrow.EscrowResolution;

namespace TruckDelivery.Payment.Application.IntegrationTests;

[Collection("PaymentIntegration")]
public sealed class EscrowPaymentTests(PaymentTestFixture fixture)
{
    private static CreateEscrowCommand BuildEscrowCommand(Guid? shipmentId = null) => new(
        ShipmentId: shipmentId ?? Guid.NewGuid(),
        OrderId: Guid.NewGuid(),
        OriginalDriverId: Guid.NewGuid(),
        ReplacementDriverId: Guid.NewGuid(),
        LockedAmount: 50_000m,
        Currency: "VND");

    [Fact]
    public async Task CreateEscrow_Should_PersistEscrow_InLockedState()
    {
        var handler = new CreateEscrowCommandHandler(
            fixture.EscrowRepository,
            fixture.UnitOfWork);

        var result = await handler.Handle(BuildEscrowCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var escrow = await fixture.EscrowRepository.GetByIdAsync(result.Value);
        escrow.Should().NotBeNull();
        escrow!.Status.Should().Be(EscrowStatus.Locked);
        escrow.LockedAmount.Should().Be(50_000m);
    }

    [Fact]
    public async Task CreateEscrow_Should_BeIdempotent_WhenCalledTwiceForSameShipment()
    {
        var shipmentId = Guid.NewGuid();
        var handler = new CreateEscrowCommandHandler(
            fixture.EscrowRepository,
            fixture.UnitOfWork);

        var first = await handler.Handle(BuildEscrowCommand(shipmentId), CancellationToken.None);
        var second = await handler.Handle(BuildEscrowCommand(shipmentId), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        // Both return the same escrow id — idempotent
        first.Value.Should().Be(second.Value);

        var count = fixture.Db.EscrowPayments.Count(e => e.ShipmentId == shipmentId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task ResolveEscrow_Confirm_Should_TransitionToReleased()
    {
        var createHandler = new CreateEscrowCommandHandler(
            fixture.EscrowRepository,
            fixture.UnitOfWork);
        var resolveHandler = new ResolveEscrowCommandHandler(
            fixture.EscrowRepository,
            fixture.UnitOfWork);

        var created = await createHandler.Handle(BuildEscrowCommand(), CancellationToken.None);
        created.IsSuccess.Should().BeTrue();

        var resolve = new ResolveEscrowCommand(created.Value, Confirm);
        var result = await resolveHandler.Handle(resolve, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var escrow = await fixture.EscrowRepository.GetByIdAsync(created.Value);
        escrow!.Status.Should().Be(EscrowStatus.Released);
    }

    [Fact]
    public async Task ResolveEscrow_Dispute_Should_TransitionToDisputed()
    {
        var createHandler = new CreateEscrowCommandHandler(
            fixture.EscrowRepository,
            fixture.UnitOfWork);
        var resolveHandler = new ResolveEscrowCommandHandler(
            fixture.EscrowRepository,
            fixture.UnitOfWork);

        var created = await createHandler.Handle(BuildEscrowCommand(), CancellationToken.None);
        created.IsSuccess.Should().BeTrue();

        var resolve = new ResolveEscrowCommand(created.Value, Dispute);
        var result = await resolveHandler.Handle(resolve, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var escrow = await fixture.EscrowRepository.GetByIdAsync(created.Value);
        escrow!.Status.Should().Be(EscrowStatus.Disputed);
    }
}
