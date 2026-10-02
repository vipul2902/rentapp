using RentApp.Api.Middleware;

namespace RentApp.Api.Configuration;

/// <summary>
/// The native mobile app does not need CORS. Browser origins are allowed only when explicitly listed
/// in Cors:AllowedOrigins (CORS_ALLOWED_ORIGINS); with no entries, no cross-origin access is granted.
/// </summary>
internal static class CorsSetup
{
    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            if (origins.Length > 0)
            {
                options.AddDefaultPolicy(policy => policy
                    .WithOrigins(origins)
                    .WithMethods("GET", "POST", "PUT", "DELETE")
                    .AllowAnyHeader()
                    .WithExposedHeaders(CorrelationIdMiddleware.HeaderName));
            }
        });

        return services;
    }
}
