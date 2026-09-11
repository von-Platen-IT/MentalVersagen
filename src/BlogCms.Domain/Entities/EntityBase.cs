namespace BlogCms.Domain.Entities;

/// <summary>
/// Common base for all persistent entities. Every entity carries a GUID primary
/// key and a UTC creation timestamp (see DataSchema.md conventions).
/// </summary>
public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
