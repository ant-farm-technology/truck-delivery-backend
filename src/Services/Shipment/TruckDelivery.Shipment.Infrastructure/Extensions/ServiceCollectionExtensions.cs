using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TruckDelivery.Shipment.Application.Consumers;
using TruckDelivery.Shipment.Domain.Repositories;
using TruckDelivery.Shipment.Infrastructure.HttpClients;
using TruckDelivery.Shipment.Infrastructure.Messaging.Kafka.Consumers;
using TruckDelivery.Shipment.Infrastructure.Persistence.Dapper;
using TruckDelivery.Shipment.Infrastructure.Persistence.EFCore;
using TruckDelivery.Shipment.Infrastructure.Persistence.EFCore.Repositories;
using TruckDelivery.Shipment.Infrastructure.Persistence.Mongo;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Infrastructure.Messaging.Outbox;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Shipment.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShipmentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddEfCore(services, configuration);
        AddMongo(services, configuration);
        AddHttpClients(services, configuration);
        AddKafkaConsumers(services, configuration);
        return services;
    }

    private static void AddEfCore(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ShipmentDb")
            ?? throw new InvalidOperationException("ConnectionStrings:ShipmentDb not configured");

        services.AddDbContext<ShipmentDbContext>(opts =>
            opts.UseMySql(connectionString, Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(connectionString)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository<ShipmentDbContext>>();
        services.AddScoped<ShipmentQueryRepository>();
        services.AddHostedService<OutboxProcessor<ShipmentDbContext>>();
    }

    private static void AddMongo(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MongoDB")
            ?? throw new InvalidOperationException("ConnectionStrings:MongoDB not configured");
        var dbName = configuration["MongoDB:Database"] ?? "truck_shipment";

        services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
        services.AddScoped<IMongoDatabase>(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase(dbName));
        services.AddScoped<ISagaRepository, SagaRepository>();
    }

    private static void AddHttpClients(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<RouteServiceClient>(c =>
            c.BaseAddress = new Uri(configuration["Services:RouteService"] ?? "http://route-service:8084"));

        services.AddHttpClient<OptimizerServiceClient>(c =>
            c.BaseAddress = new Uri(configuration["Services:OptimizerService"] ?? "http://optimizer-service:8085"));
    }

    private static void AddKafkaConsumers(IServiceCollection services, IConfiguration configuration)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");
        var groupId = configuration["Kafka:GroupId"] ?? "shipment-service";

        // Each BackgroundService consumer creates its own IConsumer via ConsumerConfig
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
                Acks = Acks.Leader,
                EnableIdempotence = true
            }).Build());

        services.AddHostedService<OrderCreatedConsumer>();
        services.AddHostedService<DriverAssignedConsumer>();
        services.AddHostedService<DispatchSagaOrchestrator>();
    }
}
