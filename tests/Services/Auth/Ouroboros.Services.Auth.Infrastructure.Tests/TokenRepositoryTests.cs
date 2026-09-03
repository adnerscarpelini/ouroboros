using Microsoft.EntityFrameworkCore;
using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Infrastructure.Tests;

public class TokenRepositoryTests
{
	private static async Task<User> SeedUserWithTokenAsync(
		AuthDbContext dbContext,
		string tokenTypeName,
		string tokenHash,
		bool validated = false
	)
	{
		var tokenType = await dbContext.TokenTypes.SingleAsync(t => t.Name == tokenTypeName);

		var user = new User(
			login: "jsilva",
			fullName: "João Silva",
			email: "joao.silva@example.com",
			passwordHash: "hashed:existing"
		);

		var token = new Token(
			tokenType: tokenType,
			user: user,
			emailMessageId: 1,
			tokenHash: tokenHash,
			expiresAt: DateTime.UtcNow.AddHours(1)
		);

		if (validated)
		{
			token.Validate();
		}

		dbContext.Users.Add(user);
		dbContext.Tokens.Add(token);

		await dbContext.SaveChangesAsync();

		return user;
	}

	[Fact]
	public async Task GetByHashAsync_WithKnownHash_LoadsTokenTypeAndUser()
	{
		var database = new InMemoryAuthDatabase();

		await using (var writeContext = database.CreateContext())
		{
			await SeedUserWithTokenAsync(
				dbContext: writeContext,
				tokenTypeName: TokenTypeNames.UserCreationValidation,
				tokenHash: "hashed:known-token"
			);
		}

		await using var readContext = database.CreateContext();

		var token = await new TokenRepository(readContext).GetByHashAsync(
			tokenHash: "hashed:known-token",
			cancellationToken: CancellationToken.None
		);

		Assert.NotNull(token);
		// As navegações precisam vir preenchidas: é delas que os casos de uso dependem para validar
		// o tipo do token e confirmar o e-mail do usuário.
		Assert.Equal(TokenTypeNames.UserCreationValidation, token.TokenType.Name);
		Assert.Equal("jsilva", token.User.Login);
		// A coluna user_id continua sendo preenchida pelo EF a partir da navegação.
		Assert.Equal(token.User.Id, token.UserId);
	}

	[Fact]
	public async Task GetByHashAsync_WithUnknownHash_ReturnsNull()
	{
		var database = new InMemoryAuthDatabase();

		await using var dbContext = database.CreateContext();

		var token = await new TokenRepository(dbContext).GetByHashAsync(
			tokenHash: "hashed:unknown-token",
			cancellationToken: CancellationToken.None
		);

		Assert.Null(token);
	}

	[Fact]
	public async Task GetPendingByUserAndTypeAsync_ReturnsOnlyPendingTokensOfThatType()
	{
		var database = new InMemoryAuthDatabase();
		User user;

		await using (var writeContext = database.CreateContext())
		{
			user = await SeedUserWithTokenAsync(
				dbContext: writeContext,
				tokenTypeName: TokenTypeNames.PasswordReset,
				tokenHash: "hashed:pending-reset-token"
			);

			var passwordResetType = await writeContext.TokenTypes.SingleAsync(t => t.Name == TokenTypeNames.PasswordReset);
			var validationType = await writeContext.TokenTypes.SingleAsync(t => t.Name == TokenTypeNames.UserCreationValidation);

			var alreadyUsedToken = new Token(
				tokenType: passwordResetType,
				user: user,
				emailMessageId: 2,
				tokenHash: "hashed:used-reset-token",
				expiresAt: DateTime.UtcNow.AddHours(1)
			);
			alreadyUsedToken.Validate();

			var otherTypeToken = new Token(
				tokenType: validationType,
				user: user,
				emailMessageId: 3,
				tokenHash: "hashed:validation-token",
				expiresAt: DateTime.UtcNow.AddHours(1)
			);

			writeContext.Tokens.Add(alreadyUsedToken);
			writeContext.Tokens.Add(otherTypeToken);

			await writeContext.SaveChangesAsync();
		}

		await using var readContext = database.CreateContext();
		var persistedUser = await readContext.Users.SingleAsync();

		var pendingTokens = await new TokenRepository(readContext).GetPendingByUserAndTypeAsync(
			user: persistedUser,
			tokenTypeName: TokenTypeNames.PasswordReset,
			cancellationToken: CancellationToken.None
		);

		var pendingToken = Assert.Single(pendingTokens);
		Assert.Equal("hashed:pending-reset-token", pendingToken.TokenHash);
	}
}
