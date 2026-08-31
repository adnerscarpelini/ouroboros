using Microsoft.EntityFrameworkCore;
using Ouroboros.Common.Domain;

namespace Ouroboros.Common.Infrastructure;

public abstract class AppDbContext : DbContext
{
	protected AppDbContext(DbContextOptions options) : base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			if (typeof(Entity).IsAssignableFrom(entityType.ClrType))
			{
				var builder = modelBuilder.Entity(entityType.ClrType);

				builder.HasIndex(nameof(Entity.ExternalId)).IsUnique();

				builder.Property(nameof(Entity.Id)).HasColumnOrder(0);
				builder.Property(nameof(Entity.ExternalId)).HasColumnOrder(1);
				builder.Property(nameof(Entity.CreatedAt)).HasColumnOrder(2);
				builder.Property(nameof(Entity.UpdatedAt)).HasColumnOrder(3);
			}
		}
	}

	public override int SaveChanges()
	{
		MarkUpdatedEntitiesAsUpdated();

		return base.SaveChanges();
	}

	public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		MarkUpdatedEntitiesAsUpdated();

		return base.SaveChangesAsync(cancellationToken);
	}

	private void MarkUpdatedEntitiesAsUpdated()
	{
		foreach (var entry in ChangeTracker.Entries<Entity>())
		{
			if (entry.State == EntityState.Modified)
			{
				entry.Entity.MarkAsUpdated();
			}
		}
	}
}
