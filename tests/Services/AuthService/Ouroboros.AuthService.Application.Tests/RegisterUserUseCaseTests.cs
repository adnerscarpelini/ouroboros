using Ouroboros.Contracts.Notifications;
using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Application.Tests;

public class RegisterUserUseCaseTests
{
	[Fact]
	public async Task RegisterAsync_WithNewLoginAndEmail_CreatesUserAndReturnsSuccess()
	{
		var context = new AuthTestContext();
		var registerUserUseCase = context.CreateRegisterUserUseCase();

		var result = await registerUserUseCase.RegisterAsync(
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
	public async Task RegisterAsync_WithNewUser_RunsEverythingInSingleTransaction()
	{
		var context = new AuthTestContext();
		var registerUserUseCase = context.CreateRegisterUserUseCase();

		await registerUserUseCase.RegisterAsync(
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
	public async Task RegisterAsync_WithLoginAlreadyInUse_ReturnsFailure()
	{
		var context = new AuthTestContext();
		context.AddUser(login: "jsilva", email: "joao.silva@example.com");

		var registerUserUseCase = context.CreateRegisterUserUseCase();

		var result = await registerUserUseCase.RegisterAsync(
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
	public async Task RegisterAsync_WithEmailAlreadyInUse_ReturnsFailure()
	{
		var context = new AuthTestContext();
		context.AddUser(login: "jsilva", email: "joao.silva@example.com");

		var registerUserUseCase = context.CreateRegisterUserUseCase();

		var result = await registerUserUseCase.RegisterAsync(
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
}
