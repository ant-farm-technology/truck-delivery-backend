using TruckDelivery.Driver.Application.IntegrationTests.Fixtures;

namespace TruckDelivery.Driver.Application.IntegrationTests;

[CollectionDefinition("DriverIntegration")]
public sealed class DriverIntegrationCollection : ICollectionFixture<DriverTestFixture>;
