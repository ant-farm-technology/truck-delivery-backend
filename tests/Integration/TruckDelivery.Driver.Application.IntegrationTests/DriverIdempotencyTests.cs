using Xunit;
using FluentAssertions;
using TruckDelivery.Driver.Application.IntegrationTests.Fixtures;

namespace TruckDelivery.Driver.Application.IntegrationTests;

[Collection("DriverIntegration")]
public sealed class DriverIdempotencyTests(DriverTestFixture fixture)
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
    public async Task IdempotencyStore_Should_PreventDuplicateConsumption_ForSameEvent()
    {
        var messageId = Guid.NewGuid();
        var processCount = 0;

        async Task SimulateConsume()
        {
            if (await fixture.IdempotencyStore.HasProcessedAsync(messageId))
                return;
            processCount++;
            await fixture.IdempotencyStore.MarkProcessedAsync(messageId);
        }

        // Two consumers receive the same DriverDocumentsSubmittedEvent
        await SimulateConsume();
        await SimulateConsume();

        processCount.Should().Be(1);
    }

    [Fact]
    public async Task IdempotencyStore_Should_TrackDifferentMessageIds_Independently()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await fixture.IdempotencyStore.MarkProcessedAsync(id1);

        (await fixture.IdempotencyStore.HasProcessedAsync(id1)).Should().BeTrue();
        (await fixture.IdempotencyStore.HasProcessedAsync(id2)).Should().BeFalse();
    }
}
