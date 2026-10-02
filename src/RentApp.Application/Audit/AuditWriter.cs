using System.Text.Json;
using System.Text.Json.Serialization;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Security;
using RentApp.Domain.Audit;

namespace RentApp.Application.Audit;

public static class AuditActions
{
    public const string OrganizationRegistered = "organization.registered";
    public const string StaffCreated = "user.staff_created";
    public const string PermissionsChanged = "user.permissions_changed";
    public const string UserDisabled = "user.disabled";
    public const string UserEnabled = "user.enabled";
    public const string PasswordReset = "user.password_reset";
    public const string AccountDeleted = "user.account_deleted";
    public const string OrganizationClosed = "organization.closed";
}

/// <summary>
/// Adds audit entries to the current unit of work, so they are saved atomically with the change they
/// describe. Details must never include passwords, tokens or identity documents.
/// </summary>
public sealed class AuditWriter(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    private static readonly JsonSerializerOptions DetailsJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Records an action by the current user in the current user's organization.</summary>
    public void Record(string action, string entityType, Guid? entityId, object? details = null) =>
        Record(currentUser.OrganizationId, currentUser.IsAuthenticated && currentUser.UserId != Guid.Empty ? currentUser.UserId : null, action, entityType, entityId, details);

    /// <summary>For flows without an authenticated caller yet (e.g. registration).</summary>
    public void Record(Guid organizationId, Guid? actorUserId, string action, string entityType, Guid? entityId, object? details = null)
    {
        var json = details is null ? null : JsonSerializer.Serialize(details, DetailsJson);
        db.AuditLogs.Add(AuditLog.Create(organizationId, actorUserId, action, entityType, entityId, json, clock.GetUtcNow()));
    }
}
