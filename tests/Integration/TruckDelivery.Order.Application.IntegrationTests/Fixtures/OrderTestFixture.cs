using Xunit;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using StackExchange.Redis;
using Testcontainers.MySql;
using Testcontainers.Redis;
using TruckDelivery.Order.Domain.Repositories;
using TruckDelivery.Order.Infrastructure.Persistence;
using TruckDelivery.Order.Infrastructure.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Infrastructure.Messaging.Kafka.Idempotency;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Order.Application.IntegrationTests.Fixtures;

public sealed class OrderTestFixture : IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .WithDatabase("truck_order")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private IConnectionMultiplexer _redisConnection = null!;

    public OrderDbContext Db { get; private set; } = null!;
    public IOrderRepository OrderRepository { get; private set; } = null!;
    public IUnitOfWork UnitOfWork { get; private set; } = null!;
    public IOutboxRepository OutboxRepository { get; private set; } = null!;
    public IIdempotencyStore IdempotencyStore { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mysql.StartAsync(), _redis.StartAsync());

        var serverVersion = ServerVersion.AutoDetect(_mysql.GetConnectionString());
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseMySql(_mysql.GetConnectionString(), serverVersion)
            .Options;

        Db = new OrderDbContext(options);
        await Db.Database.MigrateAsync();

        OrderRepository = new OrderRepository(Db);
        UnitOfWork = new UnitOfWork(Db);
        OutboxRepository = new OutboxRepository<OrderDbContext>(Db);

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
