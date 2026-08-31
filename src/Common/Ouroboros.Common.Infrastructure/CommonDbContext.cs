using Microsoft.EntityFrameworkCore;
using Ouroboros.Common.Domain;

namespace Ouroboros.Common.Infrastructure;

public sealed class CommonDbContext : AppDbContext
{
	public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
	public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

	public CommonDbContext(DbContextOptions<CommonDbContext> options) : base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.HasDefaultSchema("common");

		base.OnModelCreating(modelBuilder);
	}
}
