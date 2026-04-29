using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using StackExchange.Redis;
using TruckDelivery.Analytics.Application.Consumers;
using TruckDelivery.Analytics.Domain.Repositories;
using TruckDelivery.Analytics.Infrastructure.Metrics;
using TruckDelivery.Analytics.Infrastructure.Persistence.Mongo;

namespace TruckDelivery.Analytics.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAnalyticsInfrastructure(
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
        var dbName = configuration["MongoDB:Database"] ?? "truck_analytics";

        services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
        services.AddScoped<IMongoDatabase>(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase(dbName));

        services.AddScoped<IBreakdownIncidentRepository, BreakdownIncidentRepository>();
        services.AddScoped<IFraudAlertRepository, FraudAlertRepository>();

        services.AddHostedService<MetricsPublisherJob>();
    }

    private static void AddKafkaConsumers(IServiceCollection services, IConfiguration configuration)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");
        var groupId = configuration["Kafka:GroupId"] ?? "analytics-service";

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

        services.AddHostedService<VehicleBreakdownConsumer>();
        services.AddHostedService<BreakdownReassignmentCompletedConsumer>();
        services.AddHostedService<SuspiciousDriverPairConsumer>();
    }
}
