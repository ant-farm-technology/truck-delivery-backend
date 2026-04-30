using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            opts.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository<OrderDbContext>>();
        services.AddHostedService<OutboxProcessor<OrderDbContext>>();

        services.AddHostedService<OrderAssignedConsumer>();
        services.AddHostedService<ShipmentCompletedConsumer>();
        services.AddHostedService<PaymentCompletedConsumer>();

        return services;
    }
}
