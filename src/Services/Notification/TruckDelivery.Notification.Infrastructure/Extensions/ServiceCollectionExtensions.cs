using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TruckDelivery.Notification.Application.Commands.RegisterDevice;
using TruckDelivery.Notification.Application.Consumers;
using TruckDelivery.Notification.Application.Interfaces;
using TruckDelivery.Notification.Domain.Repositories;
using TruckDelivery.Notification.Infrastructure.Notifications;
using TruckDelivery.Notification.Infrastructure.Persistence.EFCore;
using TruckDelivery.Notification.Infrastructure.Persistence.EFCore.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Infrastructure.Messaging.Outbox;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Notification.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddEfCore(services, configuration);
        AddNotificationSenders(services, configuration);
        AddKafkaConsumers(services, configuration);
        return services;
    }

    private static void AddEfCore(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NotificationDb")
            ?? throw new InvalidOperationException("ConnectionStrings:NotificationDb not configured");

        services.AddDbContext<NotificationDbContext>(opts =>
            opts.UseMySql(connectionString, Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(connectionString)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository<NotificationDbContext>>();
        services.AddScoped<IDeviceTokenStore, DeviceTokenStore>();
        services.AddHostedService<OutboxProcessor<NotificationDbContext>>();
    }

    private static void AddNotificationSenders(IServiceCollection services, IConfiguration configuration)
    {
        // Use real FCM sender when credentials are configured; fall back to stub otherwise
        if (!string.IsNullOrWhiteSpace(configuration["Firebase:CredentialsJson"]))
            services.AddScoped<IPushNotificationSender, FcmPushSender>();
        else
            services.AddScoped<IPushNotificationSender, StubPushSender>();

        services.AddScoped<ISmsNotificationSender, StubSmsSender>();
        services.AddScoped<IEmailNotificationSender, StubEmailSender>();
    }

    private static void AddKafkaConsumers(IServiceCollection services, IConfiguration configuration)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");
        var groupId = configuration["Kafka:GroupId"] ?? "notification-service";

        // Each BackgroundService creates its own IConsumer via ConsumerConfig
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

        services.AddHostedService<DriverAssignedConsumer>();
        services.AddHostedService<ShipmentStatusUpdatedConsumer>();
        services.AddHostedService<PaymentCompletedConsumer>();
        services.AddHostedService<DriverManualReviewConsumer>();
    }
}
