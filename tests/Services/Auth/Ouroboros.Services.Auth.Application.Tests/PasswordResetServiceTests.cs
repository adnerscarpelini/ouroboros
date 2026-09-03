using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application.Tests;

public class PasswordResetServiceTests
{
	[Fact]
	public async Task RequestPasswordResetAsync_WithExistingEmail_EnqueuesEmailAndCreatesToken()
	{
		var context = new AuthTestContext();
		context.AddUser();

		var passwordResetService = context.CreatePasswordResetService();

		await passwordResetService.RequestPasswordResetAsync("joao.silva@example.com", CancellationToken.None);

		Assert.Equal("joao.silva@example.com", context.EmailQueueService.LastRecipient);
		Assert.Equal(EmailTemplateNames.PasswordReset, context.EmailTemplateRenderer.LastTemplateName);

		var token = Assert.Single(context.TokenRepository.Tokens);
		Assert.Equal(TokenTypeNames.PasswordReset, token.TokenType.Name);
		Assert.False(token.Validated);
	}

	[Fact]
	public async Task RequestPasswordResetAsync_WithExistingEmail_RunsEverythingInSingleTransaction()
	{
		var context = new AuthTestContext();
		context.AddUser();

		var passwordResetService = context.CreatePasswordResetService();

		await passwordResetService.RequestPasswordResetAsync("joao.silva@example.com", CancellationToken.None);

		// Invalidar os tokens antigos e criar o novo precisam ser atômicos: se só a invalidação fosse
		// gravada, o usuário ficaria sem nenhum token válido pra redefinir a senha.
		Assert.Equal(1, context.UnitOfWork.TransactionCount);
	}

	[Fact]
	public async Task RequestPasswordResetAsync_WithUnknownEmail_DoesNotEnqueueEmailOrCreateToken()
	{
		var context = new AuthTestContext();

		var passwordResetService = context.CreatePasswordResetService();

		await passwordResetService.RequestPasswordResetAsync("nao-existe@example.com", CancellationToken.None);

		Assert.Null(context.EmailQueueService.LastRecipient);
		Assert.Empty(context.TokenRepository.Tokens);
	}

	[Fact]
	public async Task RequestPasswordResetAsync_WithPendingToken_InvalidatesPreviousToken()
	{
		var context = new AuthTestContext();
		var user = context.AddUser();
		var previousToken = await context.AddTokenAsync(
			user: user,
			tokenTypeName: TokenTypeNames.PasswordReset,
			tokenHash: "hashed:previous-token",
			expiresAt: DateTime.UtcNow.AddHours(1)
		);

		var passwordResetService = context.CreatePasswordResetService();

		await passwordResetService.RequestPasswordResetAsync("joao.silva@example.com", CancellationToken.None);

		Assert.True(previousToken.Validated);
		Assert.Equal(2, context.TokenRepository.Tokens.Count);
	}

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

		var passwordResetService = context.CreatePasswordResetService();

		var result = await passwordResetService.ResetPasswordAsync("known-token", "new-password", CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("hashed:new-password", user.PasswordHash);
		Assert.True(token.Validated);
		Assert.NotNull(token.ValidatedAt);
	}

	[Fact]
	public async Task ResetPasswordAsync_WithUnknownToken_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var passwordResetService = context.CreatePasswordResetService();

		var result = await passwordResetService.ResetPasswordAsync("unknown-token", "new-password", CancellationToken.None);

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

		var passwordResetService = context.CreatePasswordResetService();

		var result = await passwordResetService.ResetPasswordAsync("known-token", "new-password", CancellationToken.None);

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

		var passwordResetService = context.CreatePasswordResetService();

		var result = await passwordResetService.ResetPasswordAsync("known-token", "new-password", CancellationToken.None);

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

		var passwordResetService = context.CreatePasswordResetService();

		var result = await passwordResetService.ResetPasswordAsync("known-token", "new-password", CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.Equal("hashed:existing", user.PasswordHash);
	}
}
