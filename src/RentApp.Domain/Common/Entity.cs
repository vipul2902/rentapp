namespace RentApp.Domain.Common;

/// <summary>
/// Base type for all persisted domain entities. Ids are UUIDv7 so they sort by
/// creation time, which keeps B-tree indexes compact.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
