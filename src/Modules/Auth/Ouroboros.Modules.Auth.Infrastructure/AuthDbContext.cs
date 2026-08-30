using Microsoft.EntityFrameworkCore;
using Ouroboros.Modules.Auth.Domain;

namespace Ouroboros.Modules.Auth.Infrastructure;

public sealed class AuthDbContext : DbContext
{
	public DbSet<User> Users => Set<User>();

	public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.HasDefaultSchema("auth");

		base.OnModelCreating(modelBuilder);
	}
}
