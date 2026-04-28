using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TruckDelivery.Identity.Application.Services;
using TruckDelivery.Identity.Domain.Repositories;
using TruckDelivery.Identity.Infrastructure.Authentication;
using TruckDelivery.Identity.Infrastructure.Persistence;
using TruckDelivery.Identity.Infrastructure.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Infrastructure.Messaging;
using TruckDelivery.Shared.Infrastructure.Messaging.Kafka;

namespace TruckDelivery.Identity.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var connectionString = configuration.GetConnectionString("IdentityDb") ?? throw new InvalidOperationException("ConnectionStrings:IdentityDb not configured");
        services.AddDbContext<IdentityDbContext>(opts => opts.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IJwtService, JwtService>();

        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");
        services.AddSingleton(_ => new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = bootstrapServers }).Build());
        services.AddScoped<IEventBus, KafkaEventBus>();

        return services;
    }
}
