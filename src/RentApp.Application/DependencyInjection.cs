using Microsoft.Extensions.DependencyInjection;
using RentApp.Application.Audit;
using RentApp.Application.Auth;
using RentApp.Application.Common.Time;
using RentApp.Application.Properties;
using RentApp.Application.Tenants;
using RentApp.Application.Users;

namespace RentApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuditWriter>();
        services.AddScoped<OrganizationClock>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserManagementService>();
        services.AddScoped<PropertyService>();
        services.AddScoped<RoomService>();
        services.AddScoped<TenantService>();
        return services;
    }
}
