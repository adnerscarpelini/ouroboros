using Microsoft.EntityFrameworkCore;
using Ouroboros.BuildingBlocks.Domain;
using Ouroboros.BuildingBlocks.Infrastructure;
using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Infrastructure;

public sealed class AuthDbContext : AppDbContext
{
	public DbSet<User> Users => Set<User>();
	public DbSet<TokenType> TokenTypes => Set<TokenType>();
	public DbSet<Token> Tokens => Set<Token>();
	public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
	public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
	public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

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

		// As mesmas chaves estrangeiras de antes (token_type_id/user_id), agora declaradas a partir
		// das referências navegáveis das entidades — o schema gerado é idêntico.
		modelBuilder.Entity<Token>(builder =>
		{
			builder.HasIndex(x => x.TokenHash).IsUnique();

			builder.HasOne(x => x.TokenType)
				.WithMany()
				.HasForeignKey(x => x.TokenTypeId);

			builder.HasOne(x => x.User)
				.WithMany()
				.HasForeignKey(x => x.UserId);
		});

		modelBuilder.Entity<RefreshToken>(builder =>
		{
			builder.HasIndex(x => x.TokenHash).IsUnique();

			builder.HasOne(x => x.User)
				.WithMany()
				.HasForeignKey(x => x.UserId);
		});

		modelBuilder.ApplyCommonEntities();

		base.OnModelCreating(modelBuilder);
	}
}
