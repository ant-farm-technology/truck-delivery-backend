using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using TruckDelivery.Order.Application.Consumers;
using TruckDelivery.Order.Domain.Repositories;
using TruckDelivery.Order.Infrastructure.Persistence;
using TruckDelivery.Order.Infrastructure.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Infrastructure.Messaging.Outbox;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Order.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrderInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrderDb")
            ?? throw new InvalidOperationException("ConnectionStrings:OrderDb not configured");

        services.AddDbContext<OrderDbContext>(opts =>
            opts.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                mysqlOpts => mysqlOpts.SchemaBehavior(MySqlSchemaBehavior.Ignore)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository<OrderDbContext>>();
        services.AddHostedService<TruckDelivery.Shared.Infrastructure.Persistence.DatabaseInitializerService<OrderDbContext>>();
        services.AddHostedService<OutboxProcessor<OrderDbContext>>();

        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");
        var groupId = configuration["Kafka:GroupId"] ?? "order-service";

        services.AddSingleton(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        });

        services.AddSingleton<IProducer<string, string>>(_ =>
            new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                Acks = Acks.All,
                EnableIdempotence = true
            }).Build());

        services.AddHostedService<OrderAssignedConsumer>();
        services.AddHostedService<ShipmentCompletedConsumer>();
        services.AddHostedService<PaymentCompletedConsumer>();

        return services;
    }
}
