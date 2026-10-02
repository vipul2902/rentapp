using Microsoft.AspNetCore.Authorization;
using RentApp.Application.Common.Security;
using RentApp.Domain.Users;

namespace RentApp.Api.Auth;

public static class Policies
{
    public const string OwnerOnly = "OwnerOnly";

    public static string For(StaffPermissions permission) => $"Permission:{permission}";
}

/// <summary>Protects an endpoint with a staff permission. Owners always pass.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(StaffPermissions permission) : AuthorizeAttribute(Policies.For(permission))
{
    public StaffPermissions Permission { get; } = permission;
}

internal sealed class PermissionRequirement(StaffPermissions permission) : IAuthorizationRequirement
{
    public StaffPermissions Permission { get; } = permission;
}

internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var user = context.User;
        if (user.HasClaim(AppClaimTypes.Role, nameof(UserRole.Owner))
            || user.HasClaim(AppClaimTypes.Permission, requirement.Permission.ToString()))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
