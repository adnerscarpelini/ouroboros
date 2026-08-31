namespace Ouroboros.Common.Domain;

public abstract class Entity
{
	public long Id { get; private set; }
	public Guid ExternalId { get; private set; }
	public DateTime CreatedAt { get; private set; }
	public DateTime? UpdatedAt { get; private set; }

	protected Entity()
	{
		ExternalId = Guid.NewGuid();
		CreatedAt = DateTime.UtcNow;
	}

	public void MarkAsUpdated()
	{
		UpdatedAt = DateTime.UtcNow;
	}
}
