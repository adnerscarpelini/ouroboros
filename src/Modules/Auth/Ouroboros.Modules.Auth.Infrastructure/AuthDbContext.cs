using Microsoft.EntityFrameworkCore;
using Ouroboros.Common.Infrastructure;
using Ouroboros.Modules.Auth.Domain;

namespace Ouroboros.Modules.Auth.Infrastructure;

public sealed class AuthDbContext : AppDbContext
{
	public DbSet<User> Users => Set<User>();

	public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.HasDefaultSchema("auth");

		modelBuilder.Entity<User>(builder =>
		{
			builder.HasIndex(x => x.Login).IsUnique();
			builder.HasIndex(x => x.Email).IsUnique();
		});

		base.OnModelCreating(modelBuilder);
	}
}
