using Ouroboros.Contracts.Notifications;
using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application.Tests;

public class UserRegistrationServiceTests
{
	[Fact]
	public async Task CreateUserAsync_WithNewLoginAndEmail_CreatesUserAndReturnsSuccess()
	{
		var context = new AuthTestContext();
		var userRegistrationService = context.CreateUserRegistrationService();

		var result = await userRegistrationService.CreateUserAsync(
			login: "jsilva",
			fullName: "João Silva",
			email: "joao.silva@example.com",
			password: "any-password",
			cancellationToken: CancellationToken.None
		);

		Assert.True(result.IsSuccess);
		Assert.NotEqual(Guid.Empty, result.Value);

		var createdUser = Assert.Single(context.UserRepository.Users);
		Assert.Equal("jsilva", createdUser.Login);
		Assert.Equal("hashed:any-password", createdUser.PasswordHash);
		Assert.Equal(result.Value, createdUser.ExternalId);
		Assert.False(createdUser.IsActive);

		var createdToken = Assert.Single(context.TokenRepository.Tokens);
		Assert.Same(createdUser, createdToken.User);
		Assert.Equal(TokenTypeNames.UserCreationValidation, createdToken.TokenType.Name);
		Assert.Equal("hashed:raw-token", createdToken.TokenHash);
		Assert.False(createdToken.Validated);
		Assert.True(createdToken.ExpiresAt > DateTime.UtcNow);

		// O Auth solicita a notificação; ele não monta HTML nem conhece SMTP. O que sai daqui é o
		// pedido: destinatário, template e dados — o corpo é montado pelo serviço de Notificações.
		var notificationRequest = Assert.Single(context.OutboxMessageQueue.Requests);
		Assert.Equal(NotificationMessageTypes.EmailRequested, context.OutboxMessageQueue.LastMessageType);
		Assert.Equal("joao.silva@example.com", notificationRequest.Recipient);
		Assert.Equal(EmailTemplateKeys.AuthEmailConfirmation, notificationRequest.TemplateKey);
		Assert.Contains("raw-token", notificationRequest.Data["ConfirmationUrl"]);
		Assert.Equal("João Silva", notificationRequest.Data["FullName"]);
		// Token e solicitação precisam vencer no mesmo instante: calculados em dois lugares, a
		// notificação poderia continuar sendo tentada depois de o link já ter morrido.
		Assert.Equal(createdToken.ExpiresAt, notificationRequest.ExpiresAt);
	}

	[Fact]
	public async Task CreateUserAsync_WithNewUser_RunsEverythingInSingleTransaction()
	{
		var context = new AuthTestContext();
		var userRegistrationService = context.CreateUserRegistrationService();

		await userRegistrationService.CreateUserAsync(
			login: "jsilva",
			fullName: "João Silva",
			email: "joao.silva@example.com",
			password: "any-password",
			cancellationToken: CancellationToken.None
		);

		// Sem a transação, uma falha entre gravar o usuário e gravar o token de confirmação
		// deixaria um usuário sem nenhum caminho pra confirmar o cadastro.
		Assert.Equal(1, context.UnitOfWork.TransactionCount);
	}

	[Fact]
	public async Task CreateUserAsync_WithLoginAlreadyInUse_ReturnsFailure()
	{
		var context = new AuthTestContext();
		context.AddUser(login: "jsilva", email: "joao.silva@example.com");

		var userRegistrationService = context.CreateUserRegistrationService();

		var result = await userRegistrationService.CreateUserAsync(
			login: "jsilva",
			fullName: "Outro Nome",
			email: "outro@example.com",
			password: "any-password",
			cancellationToken: CancellationToken.None
		);

		Assert.False(result.IsSuccess);
		Assert.Single(context.UserRepository.Users);
		Assert.Empty(context.TokenRepository.Tokens);
	}

	[Fact]
	public async Task CreateUserAsync_WithEmailAlreadyInUse_ReturnsFailure()
	{
		var context = new AuthTestContext();
		context.AddUser(login: "jsilva", email: "joao.silva@example.com");

		var userRegistrationService = context.CreateUserRegistrationService();

		var result = await userRegistrationService.CreateUserAsync(
			login: "outrologin",
			fullName: "Outro Nome",
			email: "joao.silva@example.com",
			password: "any-password",
			cancellationToken: CancellationToken.None
		);

		Assert.False(result.IsSuccess);
		Assert.Single(context.UserRepository.Users);
		Assert.Empty(context.TokenRepository.Tokens);
	}

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

		var userRegistrationService = context.CreateUserRegistrationService();

		var result = await userRegistrationService.ConfirmEmailAsync("known-token", CancellationToken.None);

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
		var userRegistrationService = context.CreateUserRegistrationService();

		var result = await userRegistrationService.ConfirmEmailAsync("unknown-token", CancellationToken.None);

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

		var userRegistrationService = context.CreateUserRegistrationService();

		var result = await userRegistrationService.ConfirmEmailAsync("known-token", CancellationToken.None);

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

		var userRegistrationService = context.CreateUserRegistrationService();

		var result = await userRegistrationService.ConfirmEmailAsync("known-token", CancellationToken.None);

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

		var userRegistrationService = context.CreateUserRegistrationService();

		var result = await userRegistrationService.ConfirmEmailAsync("known-token", CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.False(user.IsActive);
	}
}
