using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Application.Tests;

public class ConfirmEmailUseCaseTests
{
	[Fact]
	public async Task ConfirmEmailAsync_WithValidToken_ActivatesUserAndValidatesToken()
	{
		var context = new AuthTestContext();
		var user = context.AddUser(confirmEmail: false);
		await context.AddTokenAsync(
			user: user,
			tokenTypeName: TokenTypeNames.UserCreationValidation,
			tokenHash: "hashed:known-token",
			expiresAt: DateTime.UtcNow.AddHours(1)
		);

		var confirmEmailUseCase = context.CreateConfirmEmailUseCase();

		var result = await confirmEmailUseCase.ConfirmEmailAsync("known-token", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.True(user.IsActive);
		Assert.True(user.EmailConfirmed);

		var token = Assert.Single(context.TokenRepository.Tokens);
		Assert.True(token.Validated);
		Assert.NotNull(token.ValidatedAt);
	}

	[Fact]
	public async Task ConfirmEmailAsync_WithUnknownToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var confirmEmailUseCase = context.CreateConfirmEmailUseCase();

		var result = await confirmEmailUseCase.ConfirmEmailAsync("unknown-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}

	[Fact]
	public async Task ConfirmEmailAsync_WithAlreadyValidatedToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var user = context.AddUser(confirmEmail: false);
		await context.AddTokenAsync(
			user: user,
			tokenTypeName: TokenTypeNames.UserCreationValidation,
			tokenHash: "hashed:known-token",
			expiresAt: DateTime.UtcNow.AddHours(1),
			validated: true
		);

		var confirmEmailUseCase = context.CreateConfirmEmailUseCase();

		var result = await confirmEmailUseCase.ConfirmEmailAsync("known-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.False(user.IsActive);
	}

	[Fact]
	public async Task ConfirmEmailAsync_WithExpiredToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var user = context.AddUser(confirmEmail: false);
		await context.AddTokenAsync(
			user: user,
			tokenTypeName: TokenTypeNames.UserCreationValidation,
			tokenHash: "hashed:known-token",
			expiresAt: DateTime.UtcNow.AddHours(-1)
		);

		var confirmEmailUseCase = context.CreateConfirmEmailUseCase();

		var result = await confirmEmailUseCase.ConfirmEmailAsync("known-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.False(user.IsActive);
	}

	[Fact]
	public async Task ConfirmEmailAsync_WithPasswordResetToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var user = context.AddUser(confirmEmail: false);
		await context.AddTokenAsync(
			user: user,
			tokenTypeName: TokenTypeNames.PasswordReset,
			tokenHash: "hashed:known-token",
			expiresAt: DateTime.UtcNow.AddHours(1)
		);

		var confirmEmailUseCase = context.CreateConfirmEmailUseCase();

		var result = await confirmEmailUseCase.ConfirmEmailAsync("known-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.False(user.IsActive);
	}
}
