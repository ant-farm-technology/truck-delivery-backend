using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TruckDelivery.Shared.Infrastructure.Caching;
using TruckDelivery.Shared.Infrastructure.Caching.Redis;
using TruckDelivery.Shared.Infrastructure.Messaging;
using TruckDelivery.Shared.Infrastructure.Messaging.Kafka;
using TruckDelivery.Shared.Infrastructure.Messaging.Kafka.Idempotency;
using TruckDelivery.Shared.Infrastructure.Persistence;
using TruckDelivery.Shared.Infrastructure.Persistence.MySql;

namespace TruckDelivery.Shared.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRedis(configuration);
        services.AddKafkaProducer(configuration);
        services.AddMySqlConnectionFactory(configuration);
        return services;
    }

    private static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Redis connection string is not configured.");
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<IIdempotencyStore, RedisIdempotencyStore>();
        return services;
    }

    private static IServiceCollection AddKafkaProducer(this IServiceCollection services, IConfiguration configuration)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? throw new InvalidOperationException("Kafka:BootstrapServers is not configured.");
        services.AddSingleton(_ => new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = bootstrapServers }).Build());
        services.AddScoped<IEventBus, KafkaEventBus>();
        return services;
    }

    private static IServiceCollection AddMySqlConnectionFactory(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MySQL") ?? throw new InvalidOperationException("MySQL connection string is not configured.");
        services.AddScoped<IDbConnectionFactory>(_ => new MySqlConnectionFactory(connectionString));
        return services;
    }
}
