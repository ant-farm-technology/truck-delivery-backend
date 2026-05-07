using Xunit;
using FluentAssertions;
using TruckDelivery.Order.Application.IntegrationTests.Fixtures;
using TruckDelivery.Order.Domain.ValueObjects;

namespace TruckDelivery.Order.Application.IntegrationTests;

[Collection("OrderIntegration")]
public sealed class OrderRepositoryTests(OrderTestFixture fixture)
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static Address ValidPickup => Address.Create("123 Nguyen Trai", "Ho Chi Minh", "Ho Chi Minh", "70000", "VN").Value;
    private static Address ValidDelivery => Address.Create("456 Le Loi", "Ha Noi", "Ha Noi", "10000", "VN").Value;

    [Fact]
    public async Task Repository_Should_PersistOrder_AndRetrieveById()
    {
        var items = new List<(string, int, decimal, decimal, decimal?, decimal?, decimal?, bool, string?)>
        {
            ("Thung hang", 2, 50m, 0.5m, 1.0m, 0.8m, 0.6m, false, null)
        };

        var order = Domain.Aggregates.Order.Create(CustomerId, ValidPickup, ValidDelivery, items).Value;
        await fixture.OrderRepository.AddAsync(order);
        await fixture.UnitOfWork.SaveChangesAsync();

        var retrieved = await fixture.OrderRepository.GetByIdAsync(order.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(order.Id);
        retrieved.CustomerId.Should().Be(CustomerId);
        retrieved.Status.Should().Be(TruckDelivery.Order.Domain.ValueObjects.OrderStatus.Pending);
        retrieved.TotalWeightKg.Should().Be(100m); // 2 Ã— 50
    }

    [Fact]
    public async Task Repository_Should_Include_OrderItems_WhenRetrieved()
    {
        var items = new List<(string, int, decimal, decimal, decimal?, decimal?, decimal?, bool, string?)>
        {
            ("Item A", 1, 10m, 0.2m, null, null, null, false, null),
            ("Item B", 3, 5m, 0.1m, null, null, null, false, null)
        };

        var order = Domain.Aggregates.Order.Create(CustomerId, ValidPickup, ValidDelivery, items).Value;
        await fixture.OrderRepository.AddAsync(order);
        await fixture.UnitOfWork.SaveChangesAsync();

        var retrieved = await fixture.OrderRepository.GetByIdAsync(order.Id);

        retrieved!.Items.Should().HaveCount(2);
        retrieved.Items.Should().Contain(i => i.ProductName == "Item A");
        retrieved.Items.Should().Contain(i => i.ProductName == "Item B");
    }

    [Fact]
    public async Task Repository_Should_UpdateStatus_WhenOrderUpdated()
    {
        var items = new List<(string, int, decimal, decimal, decimal?, decimal?, decimal?, bool, string?)>
        {
            ("Box", 1, 20m, 0.3m, null, null, null, false, null)
        };

        var order = Domain.Aggregates.Order.Create(CustomerId, ValidPickup, ValidDelivery, items).Value;
        await fixture.OrderRepository.AddAsync(order);
        await fixture.UnitOfWork.SaveChangesAsync();

        order.UpdateStatus(TruckDelivery.Order.Domain.ValueObjects.OrderStatus.Confirmed);
        fixture.OrderRepository.Update(order);
        await fixture.UnitOfWork.SaveChangesAsync();

        var updated = await fixture.OrderRepository.GetByIdAsync(order.Id);
        updated!.Status.Should().Be(TruckDelivery.Order.Domain.ValueObjects.OrderStatus.Confirmed);
    }

    [Fact]
    public async Task Repository_Should_ReturnNull_ForNonExistentOrder()
    {
        var result = await fixture.OrderRepository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}
