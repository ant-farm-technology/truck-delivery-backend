using Xunit;
using TruckDelivery.Payment.Application.IntegrationTests.Fixtures;

namespace TruckDelivery.Payment.Application.IntegrationTests;

[CollectionDefinition("PaymentIntegration")]
public sealed class PaymentIntegrationCollection : ICollectionFixture<PaymentTestFixture>;
