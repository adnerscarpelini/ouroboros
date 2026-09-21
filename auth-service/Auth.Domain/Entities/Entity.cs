namespace Ouroboros.Auth.Domain.Entities;

public abstract class Entity
{
    public long Id { get; private set; }

    public Guid ExternalId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    protected Entity()
    {
        ExternalId = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    protected void RestorePersistence(long id, Guid externalId, DateTimeOffset createdAt, DateTimeOffset? updatedAt)
    {
        Id = id;
        ExternalId = externalId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public void MarkAsUpdated()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
