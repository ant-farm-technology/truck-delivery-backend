using TruckDelivery.Shipment.Application.IntegrationTests.Fixtures;

namespace TruckDelivery.Shipment.Application.IntegrationTests;

[CollectionDefinition("ShipmentIntegration")]
public sealed class ShipmentIntegrationCollection : ICollectionFixture<ShipmentTestFixture>;
