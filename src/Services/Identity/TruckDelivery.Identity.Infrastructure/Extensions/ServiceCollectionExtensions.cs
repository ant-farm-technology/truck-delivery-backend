using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using TruckDelivery.Identity.Application.Services;
using TruckDelivery.Identity.Domain.Repositories;
using TruckDelivery.Identity.Infrastructure.Authentication;
using TruckDelivery.Identity.Infrastructure.Persistence;
using TruckDelivery.Identity.Infrastructure.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Infrastructure.Messaging.Outbox;
using TruckDelivery.Shared.Infrastructure.Persistence;
using TruckDelivery.Shared.Infrastructure.Persistence.MySql;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Identity.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var connectionString = configuration.GetConnectionString("IdentityDb") ?? throw new InvalidOperationException("ConnectionStrings:IdentityDb not configured");
        services.AddDbContext<IdentityDbContext>(opts => opts.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
            mysqlOpts => mysqlOpts.SchemaBehavior(MySqlSchemaBehavior.Ignore)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDbConnectionFactory>(_ => new MySqlConnectionFactory(connectionString));
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IOutboxRepository, OutboxRepository<IdentityDbContext>>();

        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");
        services.AddSingleton<IProducer<string, string>>(_ => new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build());
        services.AddHostedService<DatabaseInitializerService>();
        services.AddHostedService<OutboxProcessor<IdentityDbContext>>();

        return services;
    }
}
