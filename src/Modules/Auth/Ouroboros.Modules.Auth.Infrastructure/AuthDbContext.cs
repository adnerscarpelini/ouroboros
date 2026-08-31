using Microsoft.EntityFrameworkCore;
using Ouroboros.Common.Infrastructure;
using Ouroboros.Modules.Auth.Domain;

namespace Ouroboros.Modules.Auth.Infrastructure;

public sealed class AuthDbContext : AppDbContext
{
	public DbSet<User> Users => Set<User>();
	public DbSet<TokenType> TokenTypes => Set<TokenType>();
	public DbSet<Token> Tokens => Set<Token>();

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

		modelBuilder.Entity<TokenType>(builder =>
		{
			builder.HasIndex(x => x.Name).IsUnique();

			builder.HasData(new
			{
				Id = 1L,
				ExternalId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
				CreatedAt = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
				UpdatedAt = (DateTime?)null,
				Name = TokenTypeNames.UserCreationValidation
			});

			builder.HasData(new
			{
				Id = 2L,
				ExternalId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
				CreatedAt = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
				UpdatedAt = (DateTime?)null,
				Name = TokenTypeNames.PasswordReset
			});
		});

		modelBuilder.Entity<Token>(builder =>
		{
			builder.HasIndex(x => x.TokenHash).IsUnique();

			builder.HasOne<TokenType>()
				.WithMany()
				.HasForeignKey(x => x.TokenTypeId);

			builder.HasOne<User>()
				.WithMany()
				.HasForeignKey(x => x.UserId);
		});

		base.OnModelCreating(modelBuilder);
	}
}
