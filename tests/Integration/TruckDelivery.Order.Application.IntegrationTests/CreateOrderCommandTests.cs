using FluentAssertions;
using TruckDelivery.Order.Application.Commands.CreateOrder;
using TruckDelivery.Order.Application.DTOs;
using TruckDelivery.Order.Application.IntegrationTests.Fixtures;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Order.Application.IntegrationTests;

[Collection("OrderIntegration")]
public sealed class CreateOrderCommandTests(OrderTestFixture fixture)
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static CreateOrderCommand BuildCommand(Guid? customerId = null) =>
        new(
            CustomerId: customerId ?? CustomerId,
            PickupAddress: new AddressRequest("123 Nguyen Trai", "Ho Chi Minh", "Ho Chi Minh", "70000", "VN"),
            DeliveryAddress: new AddressRequest("456 Le Loi", "Ha Noi", "Ha Noi", "10000", "VN"),
            Items:
            [
                new OrderItemRequest("Thung hang", 2, 50m, 0.5m, 1.0m, 0.8m, 0.6m, false, null)
            ]);

    [Fact]
    public async Task Handle_Should_PersistOrder_ToMySql()
    {
        var handler = new CreateOrderCommandHandler(
            fixture.OrderRepository,
            fixture.UnitOfWork,
            fixture.OutboxRepository);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await fixture.OrderRepository.GetByIdAsync(result.Value.OrderId);
        persisted.Should().NotBeNull();
        persisted!.CustomerId.Should().Be(CustomerId);
        persisted.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_Should_CreateOutboxEntry_ForOrderCreatedEvent()
    {
        var handler = new CreateOrderCommandHandler(
            fixture.OrderRepository,
            fixture.UnitOfWork,
            fixture.OutboxRepository);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        // Outbox entry must exist for the OrderCreated event
        var unprocessed = await fixture.OutboxRepository.GetUnprocessedAsync(10);
        unprocessed.Should().Contain(m => m.EventType.Contains("OrderCreated"));
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenItemsEmpty()
    {
        var handler = new CreateOrderCommandHandler(
            fixture.OrderRepository,
            fixture.UnitOfWork,
            fixture.OutboxRepository);

        var command = new CreateOrderCommand(
            CustomerId: CustomerId,
            PickupAddress: new AddressRequest("A", "HCM", "HCM", "70000", "VN"),
            DeliveryAddress: new AddressRequest("B", "HN", "HN", "10000", "VN"),
            Items: []);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.Items");
    }

    [Fact]
    public async Task Handle_Should_SetCorrectCreatedAt_Timestamp()
    {
        var before = DateTime.UtcNow;
        var handler = new CreateOrderCommandHandler(
            fixture.OrderRepository,
            fixture.UnitOfWork,
            fixture.OutboxRepository);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);
        var after = DateTime.UtcNow;

        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedAt.Should().BeAfter(before).And.BeBefore(after);
    }
}
