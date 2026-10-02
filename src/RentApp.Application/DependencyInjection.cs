using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RentApp.Application.Audit;
using RentApp.Application.Auth;
using RentApp.Application.Common.Time;
using RentApp.Application.Properties;
using RentApp.Application.Rent;
using RentApp.Application.Tenants;
using RentApp.Application.Users;

namespace RentApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Business dates ("today") come from this clock; tests replace it to pin the date.
        services.TryAddKeyedSingleton<TimeProvider>(BusinessTime.Key, (sp, _) => sp.GetRequiredService<TimeProvider>());

        services.AddScoped<AuditWriter>();
        services.AddScoped<OrganizationClock>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserManagementService>();
        services.AddScoped<PropertyService>();
        services.AddScoped<RoomService>();
        services.AddScoped<TenantService>();
        services.AddScoped<RentChargeGenerator>();
        services.AddScoped<RentService>();
        return services;
    }
}
