namespace RentApp.Domain.Common;

/// <summary>
/// Entities whose CreatedAt/UpdatedAt timestamps are stamped automatically on save (UTC).
/// </summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
