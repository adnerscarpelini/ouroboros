using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Application.Tests;

public class ResetPasswordUseCaseTests
{
	[Fact]
	public async Task ResetPasswordAsync_WithValidToken_UpdatesPasswordAndValidatesToken()
	{
		var context = new AuthTestContext();
		var user = context.AddUser();
		var token = await context.AddTokenAsync(
			user: user,
			tokenTypeName: TokenTypeNames.PasswordReset,
			tokenHash: "hashed:known-token",
			expiresAt: DateTime.UtcNow.AddHours(1)
		);

		var resetPasswordUseCase = context.CreateResetPasswordUseCase();

		var result = await resetPasswordUseCase.ResetPasswordAsync("known-token", "new-password", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("hashed:new-password", user.PasswordHash);
		Assert.True(token.Validated);
		Assert.NotNull(token.ValidatedAt);
	}

	[Fact]
	public async Task ResetPasswordAsync_WithUnknownToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var resetPasswordUseCase = context.CreateResetPasswordUseCase();

		var result = await resetPasswordUseCase.ResetPasswordAsync("unknown-token", "new-password", CancellationToken.None);

		Assert.False(result.IsSuccess);
	}

	[Fact]
	public async Task ResetPasswordAsync_WithAlreadyValidatedToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var user = context.AddUser();
		await context.AddTokenAsync(
			user: user,
			tokenTypeName: TokenTypeNames.PasswordReset,
			tokenHash: "hashed:known-token",
			expiresAt: DateTime.UtcNow.AddHours(1),
			validated: true
		);

		var resetPasswordUseCase = context.CreateResetPasswordUseCase();

		var result = await resetPasswordUseCase.ResetPasswordAsync("known-token", "new-password", CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.Equal("hashed:existing", user.PasswordHash);
	}

	[Fact]
	public async Task ResetPasswordAsync_WithExpiredToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var user = context.AddUser();
		await context.AddTokenAsync(
			user: user,
			tokenTypeName: TokenTypeNames.PasswordReset,
			tokenHash: "hashed:known-token",
			expiresAt: DateTime.UtcNow.AddHours(-1)
		);

		var resetPasswordUseCase = context.CreateResetPasswordUseCase();

		var result = await resetPasswordUseCase.ResetPasswordAsync("known-token", "new-password", CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.Equal("hashed:existing", user.PasswordHash);
	}

	[Fact]
	public async Task ResetPasswordAsync_WithUserCreationValidationToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var user = context.AddUser();
		await context.AddTokenAsync(
			user: user,
			tokenTypeName: TokenTypeNames.UserCreationValidation,
			tokenHash: "hashed:known-token",
			expiresAt: DateTime.UtcNow.AddHours(1)
		);

		var resetPasswordUseCase = context.CreateResetPasswordUseCase();

		var result = await resetPasswordUseCase.ResetPasswordAsync("known-token", "new-password", CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.Equal("hashed:existing", user.PasswordHash);
	}
}
