using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using StackExchange.Redis;
using TruckDelivery.Driver.Application.Consumers;
using TruckDelivery.Driver.Application.Interfaces;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Driver.Infrastructure.Jobs;
using TruckDelivery.Driver.Infrastructure.Persistence;
using TruckDelivery.Driver.Infrastructure.Repositories;
using TruckDelivery.Driver.Infrastructure.Services;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Infrastructure.Messaging;
using TruckDelivery.Shared.Infrastructure.Messaging.Kafka;
using TruckDelivery.Shared.Infrastructure.Messaging.Outbox;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Driver.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDriverInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DriverDb")
            ?? throw new InvalidOperationException("ConnectionStrings:DriverDb not configured");

        services.AddDbContext<DriverDbContext>(opts =>
            opts.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IBreakdownReportRepository, BreakdownReportRepository>();
        services.AddScoped<IDriverSwapRecordRepository, DriverSwapRecordRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository<DriverDbContext>>();

        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis not configured");
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
        services.AddScoped<IBreakdownFraudGate, BreakdownFraudGate>();

        var minioEndpoint = configuration["MinIO:Endpoint"] ?? "localhost:9000";
        var minioAccessKey = configuration["MinIO:AccessKey"] ?? throw new InvalidOperationException("MinIO:AccessKey not configured");
        var minioSecretKey = configuration["MinIO:SecretKey"] ?? throw new InvalidOperationException("MinIO:SecretKey not configured");
        var minioUseSsl = bool.Parse(configuration["MinIO:UseSsl"] ?? "false");

        services.AddSingleton<IMinioClient>(_ =>
            new MinioClient()
                .WithEndpoint(minioEndpoint)
                .WithCredentials(minioAccessKey, minioSecretKey)
                .WithSSL(minioUseSsl)
                .Build());
        services.AddScoped<IStorageService, MinIOStorageService>();

        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");
        var groupId = configuration["Kafka:GroupId"] ?? "driver-service";

        services.AddSingleton<IProducer<string, string>>(_ =>
            new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                Acks = Acks.Leader,
                EnableIdempotence = true
            }).Build());

        services.AddScoped<IEventBus, KafkaEventBus>();

        // Each BackgroundService consumer creates its own IConsumer via ConsumerConfig
        services.AddSingleton(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        });

        services.AddHostedService<UserRegisteredConsumer>();
        services.AddHostedService<BreakdownReassignmentConsumer>();
        services.AddHostedService<DriverOcrVerificationCompletedConsumer>();
        services.AddHostedService<FraudPatternAnalyzerJob>();
        services.AddHostedService<OutboxProcessor<DriverDbContext>>();

        return services;
    }
}
