using Microsoft.EntityFrameworkCore;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public sealed class BuildingBlocksDbContext : DbContext
{
	public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

	public BuildingBlocksDbContext(DbContextOptions<BuildingBlocksDbContext> options) : base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.HasDefaultSchema("shared");

		base.OnModelCreating(modelBuilder);
	}
}
