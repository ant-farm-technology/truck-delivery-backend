using Xunit;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using StackExchange.Redis;
using Testcontainers.MySql;
using Testcontainers.Redis;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Driver.Infrastructure.Persistence;
using TruckDelivery.Driver.Infrastructure.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Infrastructure.Messaging.Kafka.Idempotency;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Driver.Application.IntegrationTests.Fixtures;

public sealed class DriverTestFixture : IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .WithDatabase("truck_driver")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private IConnectionMultiplexer _redisConnection = null!;

    public DriverDbContext Db { get; private set; } = null!;
    public IDriverRepository DriverRepository { get; private set; } = null!;
    public IVehicleRepository VehicleRepository { get; private set; } = null!;
    public IUnitOfWork UnitOfWork { get; private set; } = null!;
    public IOutboxRepository OutboxRepository { get; private set; } = null!;
    public IIdempotencyStore IdempotencyStore { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mysql.StartAsync(), _redis.StartAsync());

        var serverVersion = ServerVersion.AutoDetect(_mysql.GetConnectionString());
        var options = new DbContextOptionsBuilder<DriverDbContext>()
            .UseMySql(_mysql.GetConnectionString(), serverVersion)
            .Options;

        Db = new DriverDbContext(options);
        await Db.Database.MigrateAsync();

        DriverRepository = new DriverRepository(Db);
        VehicleRepository = new VehicleRepository(Db);
        UnitOfWork = new UnitOfWork(Db);
        OutboxRepository = new OutboxRepository<DriverDbContext>(Db);

        _redisConnection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        IdempotencyStore = new RedisIdempotencyStore(_redisConnection);
    }

    public async Task DisposeAsync()
    {
        _redisConnection?.Dispose();
        await Db.DisposeAsync();
        await Task.WhenAll(_mysql.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }
}
