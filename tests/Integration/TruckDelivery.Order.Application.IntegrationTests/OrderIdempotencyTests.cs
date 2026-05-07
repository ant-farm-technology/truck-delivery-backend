using Xunit;
using FluentAssertions;
using TruckDelivery.Order.Application.IntegrationTests.Fixtures;

namespace TruckDelivery.Order.Application.IntegrationTests;

[Collection("OrderIntegration")]
public sealed class OrderIdempotencyTests(OrderTestFixture fixture)
{
    [Fact]
    public async Task IdempotencyStore_Should_ReturnFalse_ForNewMessageId()
    {
        var messageId = Guid.NewGuid();

        var hasProcessed = await fixture.IdempotencyStore.HasProcessedAsync(messageId);

        hasProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task IdempotencyStore_Should_ReturnTrue_AfterMarkingProcessed()
    {
        var messageId = Guid.NewGuid();

        await fixture.IdempotencyStore.MarkProcessedAsync(messageId);
        var hasProcessed = await fixture.IdempotencyStore.HasProcessedAsync(messageId);

        hasProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task IdempotencyStore_Should_PreventDuplicateProcessing_ForSameMessageId()
    {
        var messageId = Guid.NewGuid();
        var processCount = 0;

        async Task ProcessOnce()
        {
            if (await fixture.IdempotencyStore.HasProcessedAsync(messageId))
                return;

            processCount++;
            await fixture.IdempotencyStore.MarkProcessedAsync(messageId);
        }

        // Simulate two consumers receiving the same event
        await ProcessOnce();
        await ProcessOnce();

        processCount.Should().Be(1);
    }

    [Fact]
    public async Task IdempotencyStore_Should_TrackDifferentMessageIds_Independently()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await fixture.IdempotencyStore.MarkProcessedAsync(id1);

        var id1Processed = await fixture.IdempotencyStore.HasProcessedAsync(id1);
        var id2Processed = await fixture.IdempotencyStore.HasProcessedAsync(id2);

        id1Processed.Should().BeTrue();
        id2Processed.Should().BeFalse();
    }
}
