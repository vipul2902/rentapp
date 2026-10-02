using RentApp.Domain.Common;

namespace RentApp.Domain.Audit;

/// <summary>Append-only record of a significant change. Never updated or deleted.</summary>
public sealed class AuditLog : Entity, IOrganizationScoped
{
    public const int ActionMaxLength = 64;
    public const int EntityTypeMaxLength = 64;

    private AuditLog()
    {
    }

    public Guid OrganizationId { get; private set; }

    /// <summary>The user who made the change; null for system actions.</summary>
    public Guid? ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public Guid? EntityId { get; private set; }

    /// <summary>JSON with action-specific details. Must not contain passwords, tokens or identity documents.</summary>
    public string? Details { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static AuditLog Create(
        Guid organizationId, Guid? actorUserId, string action, string entityType, Guid? entityId, string? details, DateTimeOffset at) => new()
    {
        OrganizationId = organizationId,
        ActorUserId = actorUserId,
        Action = action,
        EntityType = entityType,
        EntityId = entityId,
        Details = details,
        CreatedAt = at,
    };
}
