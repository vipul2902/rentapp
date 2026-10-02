using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RentApp.Application.Common.Security;
using RentApp.Application.Payments;
using RentApp.Domain.Users;
using RentApp.Infrastructure.Security;

namespace RentApp.Api.Auth;

internal static class AuthSetup
{
    public static IServiceCollection AddApiAuth(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<HttpCurrentUser>();
        services.AddScoped<CurrentUserOverride>();
        services.AddScoped<ICurrentUser>(sp =>
            sp.GetRequiredService<CurrentUserOverride>().User ?? sp.GetRequiredService<HttpCurrentUser>());

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(jwt.SecretBytes),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AppClaimTypes.Subject,
                    RoleClaimType = AppClaimTypes.Role,
                };
            });

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        var authorization = services.AddAuthorizationBuilder()
            // Deny by default: every endpoint requires a signed-in user unless it opts out with [AllowAnonymous].
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.OwnerOnly, p => p.RequireAuthenticatedUser().RequireRole(nameof(UserRole.Owner)));

        // Each permission on its own, plus "any of" combinations used by endpoints.
        foreach (var permission in StaffPermissions.All.ToList().Append(ReceiptService.Access))
        {
            authorization.AddPolicy(Policies.For(permission), p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission)));
        }

        return services;
    }
}
