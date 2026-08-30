using Microsoft.EntityFrameworkCore;
using Ouroboros.Common.Domain;

namespace Ouroboros.Common.Infrastructure;

public sealed class CommonDbContext : DbContext
{
	public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

	public CommonDbContext(DbContextOptions<CommonDbContext> options) : base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.HasDefaultSchema("shared");

		base.OnModelCreating(modelBuilder);
	}
}
