using Microsoft.EntityFrameworkCore;
using Testcontainers.MySql;
using TruckDelivery.Payment.Domain.Repositories;
using TruckDelivery.Payment.Infrastructure.Persistence.EFCore;
using TruckDelivery.Payment.Infrastructure.Persistence.EFCore.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Payment.Application.IntegrationTests.Fixtures;

public sealed class PaymentTestFixture : IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .WithDatabase("truck_payment")
        .Build();

    public PaymentDbContext Db { get; private set; } = null!;
    public IPaymentRepository PaymentRepository { get; private set; } = null!;
    public IEscrowPaymentRepository EscrowRepository { get; private set; } = null!;
    public IUnitOfWork UnitOfWork { get; private set; } = null!;
    public IOutboxRepository OutboxRepository { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _mysql.StartAsync();

        var serverVersion = ServerVersion.AutoDetect(_mysql.GetConnectionString());
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseMySql(_mysql.GetConnectionString(), serverVersion)
            .Options;

        Db = new PaymentDbContext(options);
        await Db.Database.MigrateAsync();

        PaymentRepository = new PaymentRepository(Db);
        EscrowRepository = new EscrowPaymentRepository(Db);
        UnitOfWork = new UnitOfWork(Db);
        OutboxRepository = new OutboxRepository<PaymentDbContext>(Db);
    }

    public async Task DisposeAsync()
    {
        await Db.DisposeAsync();
        await _mysql.DisposeAsync();
    }
}
