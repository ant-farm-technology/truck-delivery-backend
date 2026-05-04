using System.Data;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using TruckDelivery.Payment.Application.Consumers;
using TruckDelivery.Payment.Application.Interfaces;
using TruckDelivery.Payment.Domain.Repositories;
using TruckDelivery.Payment.Infrastructure.Gateways;
using TruckDelivery.Payment.Infrastructure.Persistence.EFCore;
using TruckDelivery.Payment.Infrastructure.Persistence.EFCore.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Infrastructure.Messaging.Kafka;
using TruckDelivery.Shared.Infrastructure.Messaging.Outbox;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Payment.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddEfCore(services, configuration);
        AddGateways(services);
        AddKafkaConsumers(services, configuration);
        return services;
    }

    private static void AddGateways(IServiceCollection services)
    {
        services.AddSingleton<CodGateway>();
        services.AddSingleton<VnPayGateway>();
        services.AddSingleton<IPaymentGatewayFactory, PaymentGatewayFactory>();
    }

    private static void AddEfCore(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PaymentDb")
            ?? throw new InvalidOperationException("ConnectionStrings:PaymentDb not configured");

        services.AddDbContext<PaymentDbContext>(opts =>
            opts.UseMySql(connectionString, Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(connectionString)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IEscrowPaymentRepository, EscrowPaymentRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository<PaymentDbContext>>();
        services.AddScoped<IDbConnection>(_ => new MySqlConnection(connectionString));
        services.AddHostedService<TruckDelivery.Shared.Infrastructure.Persistence.DatabaseInitializerService<PaymentDbContext>>();
        services.AddHostedService<OutboxProcessor<PaymentDbContext>>();
    }

    private static void AddKafkaConsumers(IServiceCollection services, IConfiguration configuration)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");
        var groupId = configuration["Kafka:GroupId"] ?? "payment-service";

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

        services.AddHostedService<OrderDeliveredConsumer>();
        services.AddHostedService<BreakdownReassignmentConsumer>();
    }
}
