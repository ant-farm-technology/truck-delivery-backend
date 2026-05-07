using Xunit;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Testcontainers.MySql;
using TruckDelivery.Shipment.Domain.Repositories;
using TruckDelivery.Shipment.Infrastructure.Persistence.EFCore;
using TruckDelivery.Shipment.Infrastructure.Persistence.EFCore.Repositories;
using TruckDelivery.Shipment.Infrastructure.Persistence.Mongo;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Shipment.Application.IntegrationTests.Fixtures;

public sealed class ShipmentTestFixture : IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .WithDatabase("truck_shipment")
        .Build();

    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:7.0")
        .Build();

    public ShipmentDbContext Db { get; private set; } = null!;
    public IShipmentRepository ShipmentRepository { get; private set; } = null!;
    public IUnitOfWork UnitOfWork { get; private set; } = null!;
    public IOutboxRepository OutboxRepository { get; private set; } = null!;
    public ISagaRepository SagaRepository { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mysql.StartAsync(), _mongo.StartAsync());

        var serverVersion = Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(_mysql.GetConnectionString());
        var options = new DbContextOptionsBuilder<ShipmentDbContext>()
            .UseMySql(_mysql.GetConnectionString(), serverVersion)
            .Options;

        Db = new ShipmentDbContext(options);
        await Db.Database.MigrateAsync();

        ShipmentRepository = new ShipmentRepository(Db);
        UnitOfWork = new UnitOfWork(Db);
        OutboxRepository = new OutboxRepository<ShipmentDbContext>(Db);

        var mongoClient = new MongoClient(_mongo.GetConnectionString());
        var mongoDb = mongoClient.GetDatabase("truck_shipment");
        SagaRepository = new SagaRepository(mongoDb);
    }

    public async Task DisposeAsync()
    {
        await Db.DisposeAsync();
        await Task.WhenAll(_mysql.DisposeAsync().AsTask(), _mongo.DisposeAsync().AsTask());
    }
}
