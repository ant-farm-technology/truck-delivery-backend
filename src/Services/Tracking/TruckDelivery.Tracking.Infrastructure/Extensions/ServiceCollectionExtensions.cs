using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using StackExchange.Redis;
using TruckDelivery.Tracking.Application;
using TruckDelivery.Tracking.Application.Consumers;
using TruckDelivery.Tracking.Application.Interfaces;
using TruckDelivery.Tracking.Domain.Repositories;
using TruckDelivery.Tracking.Infrastructure.Caching;
using TruckDelivery.Tracking.Infrastructure.Hubs;
using TruckDelivery.Tracking.Infrastructure.Messaging.Kafka.Consumers;
using TruckDelivery.Tracking.Infrastructure.Persistence.Mongo;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Tracking.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrackingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddMongo(services, configuration);
        AddKafkaConsumers(services, configuration);
        return services;
    }

    private static void AddMongo(IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis not configured");
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));

        var connectionString = configuration.GetConnectionString("MongoDB")
            ?? throw new InvalidOperationException("ConnectionStrings:MongoDB not configured");
        var dbName = configuration["MongoDB:Database"] ?? "truck_tracking";

        services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
        services.AddScoped<IMongoDatabase>(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase(dbName));

        services.AddScoped<ITrackingSessionRepository, TrackingSessionRepository>();
        services.AddScoped<ITrackingPointRepository, TrackingPointRepository>();
        services.AddScoped<IOutboxRepository, MongoOutboxRepository>();
        services.AddScoped<ITrackingNotifier, TrackingHubNotifier>();
        services.AddScoped<IDriverGpsCache, RedisDriverGpsCache>();
        services.AddHostedService<MongoOutboxProcessor>();
    }

    private static void AddKafkaConsumers(IServiceCollection services, IConfiguration configuration)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");
        var groupId = configuration["Kafka:GroupId"] ?? "tracking-service";

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

        services.AddHostedService<ShipmentStartedConsumer>();
        services.AddHostedService<ShipmentCompletedConsumer>();
    }
}
