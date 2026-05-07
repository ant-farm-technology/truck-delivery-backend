using Xunit;
using TruckDelivery.Order.Application.IntegrationTests.Fixtures;

namespace TruckDelivery.Order.Application.IntegrationTests;

// Shares a single fixture (and thus single Docker container set) across all test classes in the collection.
[CollectionDefinition("OrderIntegration")]
public sealed class OrderIntegrationCollection : ICollectionFixture<OrderTestFixture>;
