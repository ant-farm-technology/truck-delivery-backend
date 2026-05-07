using Xunit;
namespace TruckDelivery.E2E.Tests.Fixtures;

[CollectionDefinition("E2E")]
public sealed class E2ECollection : ICollectionFixture<E2ETestFixture>;
