using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Security;
using RentApp.Application.Payments;
using RentApp.Infrastructure.Caching;
using RentApp.Infrastructure.Configuration;
using RentApp.Infrastructure.Persistence;
using RentApp.Infrastructure.Receipts;
using RentApp.Infrastructure.Security;
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

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(JwtOptions.IsValid,
                $"Jwt settings are invalid: JWT_SECRET must be at least {JwtOptions.MinimumSecretBytes} bytes and JWT_ISSUER/JWT_AUDIENCE are required.")
            .ValidateOnStart();

        // A fallback for hosts without an HTTP caller; the API registers its own ICurrentUser first.
        services.TryAddScoped<ICurrentUser>(_ => AnonymousCurrentUser.Instance);

        services.AddSingleton<AuditableEntityInterceptor>();
        services.AddScoped<OrganizationIsolationInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            ConfigureNpgsql(options, sp.GetRequiredService<IOptions<ConnectionStringOptions>>().Value.Database);
            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<OrganizationIsolationInterceptor>());
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IReceiptPdfRenderer, ReceiptPdfRenderer>();

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

    /// <summary>Opens the Redis connection at startup so the first request does not pay for it.</summary>
    public static void WarmUpRedis(this IServiceProvider services) => services.GetRequiredService<IConnectionMultiplexer>();

    internal static void ConfigureNpgsql(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
}
