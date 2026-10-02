using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RentApp.Infrastructure.Caching;
using RentApp.Infrastructure.Configuration;
using RentApp.Infrastructure.Persistence;
using StackExchange.Redis;

namespace RentApp.Infrastructure;

public static class DependencyInjection
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<ConnectionStringOptions>()
            .Bind(configuration.GetSection(ConnectionStringOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Database), "ConnectionStrings:Database (DATABASE_CONNECTION_STRING) is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Redis), "ConnectionStrings:Redis (REDIS_CONNECTION_STRING) is required.")
            .ValidateOnStart();

        services.AddSingleton<AuditableEntityInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            ConfigureNpgsql(options, sp.GetRequiredService<IOptions<ConnectionStringOptions>>().Value.Database);
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        // Redis is a cache only; PostgreSQL is the source of truth. AbortOnConnectFail=false lets the
        // API start (and report itself not-ready) while Redis is down, then reconnect automatically.
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisOptions = ConfigurationOptions.Parse(sp.GetRequiredService<IOptions<ConnectionStringOptions>>().Value.Redis);
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectTimeout = 5_000;
            return ConnectionMultiplexer.Connect(redisOptions);
        });

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("postgres", tags: [ReadyTag])
            .AddCheck<RedisHealthCheck>("redis", tags: [ReadyTag]);

        return services;
    }

    public static async Task ApplyMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    internal static void ConfigureNpgsql(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
}
