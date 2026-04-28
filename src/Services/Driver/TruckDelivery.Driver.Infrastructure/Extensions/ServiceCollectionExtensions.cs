using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TruckDelivery.Driver.Application.Consumers;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Driver.Infrastructure.Persistence;
using TruckDelivery.Driver.Infrastructure.Repositories;
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
        services.AddScoped<IOutboxRepository, OutboxRepository<DriverDbContext>>();

        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");

        services.AddSingleton<IProducer<string, string>>(_ =>
            new ProducerBuilder<string, string>(
                new ProducerConfig { BootstrapServers = bootstrapServers })
            .Build());

        services.AddScoped<IEventBus, KafkaEventBus>();

        services.AddSingleton<IConsumer<string, string>>(_ =>
            new ConsumerBuilder<string, string>(
                new ConsumerConfig
                {
                    BootstrapServers = bootstrapServers,
                    GroupId = "driver-service",
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    EnableAutoCommit = false
                })
            .Build());

        services.AddHostedService<UserRegisteredConsumer>();
        services.AddHostedService<OutboxProcessor<DriverDbContext>>();

        return services;
    }
}
