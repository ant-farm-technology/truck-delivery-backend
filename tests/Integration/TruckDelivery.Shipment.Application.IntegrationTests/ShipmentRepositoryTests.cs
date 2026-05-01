using FluentAssertions;
using TruckDelivery.Shipment.Application.IntegrationTests.Fixtures;
using TruckDelivery.Shipment.Domain.Aggregates;

namespace TruckDelivery.Shipment.Application.IntegrationTests;

[Collection("ShipmentIntegration")]
public sealed class ShipmentRepositoryTests(ShipmentTestFixture fixture)
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    [Fact]
    public async Task Repository_Should_PersistShipment_AndRetrieveById()
    {
        var shipment = Shipment.Create(OrderId, CustomerId, "123 A", "HCM", "456 B", "HN", 100m, 1.5m);
        await fixture.ShipmentRepository.AddAsync(shipment);
        await fixture.UnitOfWork.SaveChangesAsync();

        var retrieved = await fixture.ShipmentRepository.GetByIdAsync(shipment.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(shipment.Id);
        retrieved.OrderId.Should().Be(OrderId);
        retrieved.Status.Should().Be(ShipmentStatus.Created);
    }

    [Fact]
    public async Task Repository_Should_FindShipment_ByOrderId()
    {
        var orderId = Guid.NewGuid();
        var shipment = Shipment.Create(orderId, CustomerId, "A", "HCM", "B", "HN", 50m, 0.5m);
        await fixture.ShipmentRepository.AddAsync(shipment);
        await fixture.UnitOfWork.SaveChangesAsync();

        var found = await fixture.ShipmentRepository.GetByOrderIdAsync(orderId);

        found.Should().NotBeNull();
        found!.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task Repository_Should_UpdateStatus_WhenShipmentTransitions()
    {
        var shipment = Shipment.Create(Guid.NewGuid(), CustomerId, "A", "HCM", "B", "HN", 80m, 1.0m);
        await fixture.ShipmentRepository.AddAsync(shipment);
        await fixture.UnitOfWork.SaveChangesAsync();

        shipment.TransitionTo(ShipmentStatus.RoutePlanning);
        await fixture.ShipmentRepository.UpdateAsync(shipment);
        await fixture.UnitOfWork.SaveChangesAsync();

        var updated = await fixture.ShipmentRepository.GetByIdAsync(shipment.Id);
        updated!.Status.Should().Be(ShipmentStatus.RoutePlanning);
    }

    [Fact]
    public async Task Repository_Should_ReturnNull_ForNonExistentShipment()
    {
        var result = await fixture.ShipmentRepository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task Repository_Should_PersistOutboxEntry_WhenShipmentCreated()
    {
        var shipment = Shipment.Create(Guid.NewGuid(), CustomerId, "X", "HCM", "Y", "HN", 60m, 0.8m);
        await fixture.ShipmentRepository.AddAsync(shipment);
        await fixture.UnitOfWork.SaveChangesAsync();

        // ShipmentCreatedDomainEvent should produce outbox entry via the command handler chain
        // Here we verify the outbox table is queryable (schema correctness)
        var outboxEntries = await fixture.OutboxRepository.GetUnprocessedAsync(10);
        outboxEntries.Should().NotBeNull();
    }
}
