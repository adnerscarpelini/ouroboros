using Ouroboros.Contracts.Notifications;
using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Application.Tests;

public class RequestPasswordResetUseCaseTests
{
	[Fact]
	public async Task RequestPasswordResetAsync_WithExistingEmail_RequestsNotificationAndCreatesToken()
	{
		var context = new AuthTestContext();
		context.AddUser();

		var requestPasswordResetUseCase = context.CreateRequestPasswordResetUseCase();

		await requestPasswordResetUseCase.RequestPasswordResetAsync("joao.silva@example.com", CancellationToken.None);

		var notificationRequest = Assert.Single(context.OutboxMessageQueue.Requests);
		Assert.Equal("joao.silva@example.com", notificationRequest.Recipient);
		Assert.Equal(EmailTemplateKeys.AuthPasswordReset, notificationRequest.TemplateKey);

		var token = Assert.Single(context.TokenRepository.Tokens);
		// O token guarda a solicitação que o leva ao usuário — correlação, não chave estrangeira.
		Assert.NotEqual(Guid.Empty, token.NotificationRequestId);
		Assert.Equal(TokenTypeNames.PasswordReset, token.TokenType.Name);
		Assert.False(token.Validated);
	}

	[Fact]
	public async Task RequestPasswordResetAsync_WithExistingEmail_RunsEverythingInSingleTransaction()
	{
		var context = new AuthTestContext();
		context.AddUser();

		var requestPasswordResetUseCase = context.CreateRequestPasswordResetUseCase();

		await requestPasswordResetUseCase.RequestPasswordResetAsync("joao.silva@example.com", CancellationToken.None);

		// Invalidar os tokens antigos e criar o novo precisam ser atômicos: se só a invalidação fosse
		// gravada, o usuário ficaria sem nenhum token válido pra redefinir a senha.
		Assert.Equal(1, context.UnitOfWork.TransactionCount);
	}

	[Fact]
	public async Task RequestPasswordResetAsync_WithUnknownEmail_DoesNotRequestNotificationOrCreateToken()
	{
		var context = new AuthTestContext();

		var requestPasswordResetUseCase = context.CreateRequestPasswordResetUseCase();

		await requestPasswordResetUseCase.RequestPasswordResetAsync("nao-existe@example.com", CancellationToken.None);

		Assert.Empty(context.OutboxMessageQueue.Requests);
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

		var requestPasswordResetUseCase = context.CreateRequestPasswordResetUseCase();

		await requestPasswordResetUseCase.RequestPasswordResetAsync("joao.silva@example.com", CancellationToken.None);

		Assert.True(previousToken.Validated);
		Assert.Equal(2, context.TokenRepository.Tokens.Count);
	}
}
