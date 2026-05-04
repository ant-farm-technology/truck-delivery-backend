using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Testcontainers.Kafka;
using Testcontainers.MongoDb;
using Testcontainers.MySql;
using Testcontainers.Redis;
using WireMock.Server;

// Use controller types as assembly markers to avoid name collision when all 5
// services define 'public partial class Program {}' in the global namespace.
using IdentityMarker = TruckDelivery.Identity.Api.Controllers.AuthController;
using OrderMarker = TruckDelivery.Order.Api.Controllers.OrdersController;
using DriverMarker = TruckDelivery.Driver.Api.Controllers.DriversController;
using ShipmentMarker = TruckDelivery.Shipment.Api.Controllers.ShipmentsController;
using PaymentMarker = TruckDelivery.Payment.Api.Controllers.PaymentsController;

namespace TruckDelivery.E2E.Tests.Fixtures;

/// <summary>
/// Shared fixture for all E2E tests. Starts real infrastructure via Testcontainers
/// and in-process service hosts via WebApplicationFactory. WireMock stubs replace
/// the Python Optimizer and Rust Route Service.
/// </summary>
public sealed class E2ETestFixture : IAsyncLifetime
{
    // ── Containers ──────────────────────────────────────────────────────────
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .WithDatabase("shared")
        .WithUsername("root")
        .WithPassword("root")
        .Build();

    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:7.0")
        .Build();

    private readonly KafkaContainer _kafka = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.6.1")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    // ── WireMock (Optimizer + Route service stubs) ───────────────────────────
    public WireMockServer WireMock { get; private set; } = null!;

    // ── WebApplicationFactories ──────────────────────────────────────────────
    private WebApplicationFactory<IdentityMarker> _identityFactory = null!;
    private WebApplicationFactory<OrderMarker> _orderFactory = null!;
    private WebApplicationFactory<DriverMarker> _driverFactory = null!;
    private WebApplicationFactory<ShipmentMarker> _shipmentFactory = null!;
    private WebApplicationFactory<PaymentMarker> _paymentFactory = null!;

    // ── HTTP Clients ─────────────────────────────────────────────────────────
    public HttpClient IdentityClient { get; private set; } = null!;
    public HttpClient OrderClient { get; private set; } = null!;
    public HttpClient DriverClient { get; private set; } = null!;
    public HttpClient ShipmentClient { get; private set; } = null!;
    public HttpClient PaymentClient { get; private set; } = null!;

    public void SetBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _mysql.StartAsync(),
            _mongo.StartAsync(),
            _kafka.StartAsync(),
            _redis.StartAsync());

        WireMock = WireMockServer.Start();

        var mysqlBase = _mysql.GetConnectionString();
        var kafkaAddr = _kafka.GetBootstrapAddress();
        var redisCs = _redis.GetConnectionString();
        var mongoCs = _mongo.GetConnectionString();
        var wireMockBase = $"http://localhost:{WireMock.Port}";

        var jwt = JwtConfig();

        _identityFactory = CreateFactory<IdentityMarker>(Merge(jwt, new()
        {
            ["ConnectionStrings:IdentityDb"] = DbCs(mysqlBase, "truck_identity_e2e"),
            ["Kafka:BootstrapServers"] = kafkaAddr,
            ["Kafka:GroupId"] = "identity-e2e",
        }));

        _orderFactory = CreateFactory<OrderMarker>(Merge(jwt, new()
        {
            ["ConnectionStrings:OrderDb"] = DbCs(mysqlBase, "truck_order_e2e"),
            ["ConnectionStrings:MySQL"] = DbCs(mysqlBase, "truck_order_e2e"),
            ["ConnectionStrings:Redis"] = redisCs,
            ["Kafka:BootstrapServers"] = kafkaAddr,
            ["Kafka:GroupId"] = "order-e2e",
        }));

        _driverFactory = CreateFactory<DriverMarker>(Merge(jwt, new()
        {
            ["ConnectionStrings:DriverDb"] = DbCs(mysqlBase, "truck_driver_e2e"),
            ["ConnectionStrings:Redis"] = redisCs,
            ["Kafka:BootstrapServers"] = kafkaAddr,
            ["Kafka:GroupId"] = "driver-e2e",
            ["MinIO:Endpoint"] = "localhost:9000",
            ["MinIO:AccessKey"] = "minioadmin",
            ["MinIO:SecretKey"] = "minioadmin",
            ["MinIO:UseSsl"] = "false",
        }));

        _shipmentFactory = CreateFactory<ShipmentMarker>(Merge(jwt, new()
        {
            ["ConnectionStrings:ShipmentDb"] = DbCs(mysqlBase, "truck_shipment_e2e"),
            ["ConnectionStrings:MySQL"] = DbCs(mysqlBase, "truck_shipment_e2e"),
            ["ConnectionStrings:MongoDB"] = mongoCs,
            ["ConnectionStrings:Redis"] = redisCs,
            ["MongoDB:Database"] = "truck_shipment_e2e",
            ["Kafka:BootstrapServers"] = kafkaAddr,
            ["Kafka:GroupId"] = "shipment-e2e",
            ["Services:RouteService"] = wireMockBase,
            ["Services:OptimizerService"] = wireMockBase,
        }));

        _paymentFactory = CreateFactory<PaymentMarker>(Merge(jwt, new()
        {
            ["ConnectionStrings:PaymentDb"] = DbCs(mysqlBase, "truck_payment_e2e"),
            ["ConnectionStrings:MySQL"] = DbCs(mysqlBase, "truck_payment_e2e"),
            ["ConnectionStrings:Redis"] = redisCs,
            ["Kafka:BootstrapServers"] = kafkaAddr,
            ["Kafka:GroupId"] = "payment-e2e",
            ["VnPay:TmnCode"] = "TEST",
            ["VnPay:HashSecret"] = "TESTHASHSECRET1234567890ABCDEFGH",
            ["VnPay:PaymentUrl"] = wireMockBase + "/vnpay",
            ["VnPay:ReturnUrl"] = "http://localhost/return",
        }));

        IdentityClient = _identityFactory.CreateClient();
        OrderClient = _orderFactory.CreateClient();
        DriverClient = _driverFactory.CreateClient();
        ShipmentClient = _shipmentFactory.CreateClient();
        PaymentClient = _paymentFactory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        WireMock.Stop();
        await Task.WhenAll(
            _identityFactory.DisposeAsync().AsTask(),
            _orderFactory.DisposeAsync().AsTask(),
            _driverFactory.DisposeAsync().AsTask(),
            _shipmentFactory.DisposeAsync().AsTask(),
            _paymentFactory.DisposeAsync().AsTask());
        await Task.WhenAll(
            _mysql.DisposeAsync().AsTask(),
            _mongo.DisposeAsync().AsTask(),
            _kafka.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask());
    }

    private static WebApplicationFactory<T> CreateFactory<T>(Dictionary<string, string?> config)
        where T : class
        => new WebApplicationFactory<T>()
            .WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Test");
                b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            });

    private static string DbCs(string baseCs, string db)
    {
        var builder = new MySqlConnectionStringBuilder(baseCs) { Database = db };
        return builder.ConnectionString;
    }

    private static Dictionary<string, string?> JwtConfig() => new()
    {
        ["Jwt:Issuer"] = Helpers.JwtHelper.TestIssuer,
        ["Jwt:Audience"] = Helpers.JwtHelper.TestAudience,
        ["Jwt:SecretKey"] = Helpers.JwtHelper.TestSecretKey,
        ["Jwt:ExpiryMinutes"] = "60",
    };

    private static Dictionary<string, string?> Merge(
        Dictionary<string, string?> a,
        Dictionary<string, string?> b)
    {
        var result = new Dictionary<string, string?>(a);
        foreach (var kv in b) result[kv.Key] = kv.Value;
        return result;
    }
}
