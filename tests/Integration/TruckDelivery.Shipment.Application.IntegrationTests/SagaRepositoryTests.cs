using FluentAssertions;
using TruckDelivery.Shipment.Application.IntegrationTests.Fixtures;
using TruckDelivery.Shipment.Infrastructure.Persistence.Mongo;

namespace TruckDelivery.Shipment.Application.IntegrationTests;

[Collection("ShipmentIntegration")]
public sealed class SagaRepositoryTests(ShipmentTestFixture fixture)
{
    [Fact]
    public async Task SagaRepository_Should_PersistState_AndRetrieveByShipmentId()
    {
        var shipmentId = Guid.NewGuid();
        var state = new ShipmentSagaState
        {
            SagaId = Guid.NewGuid(),
            ShipmentId = shipmentId,
            OrderId = Guid.NewGuid(),
            Status = ShipmentSagaStatus.Started,
            StartedAt = DateTime.UtcNow
        };

        await fixture.SagaRepository.UpsertAsync(state);

        var retrieved = await fixture.SagaRepository.GetByShipmentIdAsync(shipmentId);

        retrieved.Should().NotBeNull();
        retrieved!.ShipmentId.Should().Be(shipmentId);
        retrieved.Status.Should().Be(ShipmentSagaStatus.Started);
    }

    [Fact]
    public async Task SagaRepository_Should_UpdateExistingState_OnUpsert()
    {
        var sagaId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();

        var initial = new ShipmentSagaState
        {
            SagaId = sagaId,
            ShipmentId = shipmentId,
            OrderId = Guid.NewGuid(),
            Status = ShipmentSagaStatus.Started,
            StartedAt = DateTime.UtcNow
        };

        await fixture.SagaRepository.UpsertAsync(initial);

        // Update same saga to completed
        initial.Status = ShipmentSagaStatus.Completed;
        initial.CompletedAt = DateTime.UtcNow;
        await fixture.SagaRepository.UpsertAsync(initial);

        var retrieved = await fixture.SagaRepository.GetByShipmentIdAsync(shipmentId);

        retrieved!.Status.Should().Be(ShipmentSagaStatus.Completed);
        retrieved.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SagaRepository_Should_ReturnNull_ForNonExistentShipmentId()
    {
        var result = await fixture.SagaRepository.GetByShipmentIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task SagaRepository_Should_TrackRetryCount()
    {
        var shipmentId = Guid.NewGuid();
        var state = new ShipmentSagaState
        {
            SagaId = Guid.NewGuid(),
            ShipmentId = shipmentId,
            OrderId = Guid.NewGuid(),
            Status = ShipmentSagaStatus.Started,
            RetryCount = 0,
            StartedAt = DateTime.UtcNow
        };

        await fixture.SagaRepository.UpsertAsync(state);

        state.RetryCount = 2;
        state.Status = ShipmentSagaStatus.DriverAssigning;
        await fixture.SagaRepository.UpsertAsync(state);

        var retrieved = await fixture.SagaRepository.GetByShipmentIdAsync(shipmentId);
        retrieved!.RetryCount.Should().Be(2);
    }
}
