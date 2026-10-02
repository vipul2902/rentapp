namespace RentApp.Domain.Common;

/// <summary>
/// A record that belongs to exactly one organization. Every such entity is automatically filtered to the
/// caller's organization by a global query filter, and cross-organization writes are rejected on save.
/// </summary>
public interface IOrganizationScoped
{
    Guid OrganizationId { get; }
}
