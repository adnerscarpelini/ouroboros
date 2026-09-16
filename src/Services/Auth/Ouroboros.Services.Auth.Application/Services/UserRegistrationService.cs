using Ouroboros.BuildingBlocks.Application;
using Ouroboros.Contracts.Notifications;
using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application;

public sealed class UserRegistrationService : IUserRegistrationService
{
	private const int ValidationTokenExpirationHours = 24;

	private readonly IUserRepository _userRepository;
	private readonly ITokenRepository _tokenRepository;
	private readonly ITokenTypeRepository _tokenTypeRepository;
	private readonly IUnitOfWork _unitOfWork;
	private readonly IPasswordHasher _passwordHasher;
	private readonly ITokenGenerator _tokenGenerator;
	private readonly IOutboxMessageQueue _outboxMessageQueue;
	private readonly AuthApplicationOptions _options;

	public UserRegistrationService(
		IUserRepository userRepository,
		ITokenRepository tokenRepository,
		ITokenTypeRepository tokenTypeRepository,
		IUnitOfWork unitOfWork,
		IPasswordHasher passwordHasher,
		ITokenGenerator tokenGenerator,
		IOutboxMessageQueue outboxMessageQueue,
		AuthApplicationOptions options
	)
	{
		_userRepository = userRepository;
		_tokenRepository = tokenRepository;
		_tokenTypeRepository = tokenTypeRepository;
		_unitOfWork = unitOfWork;
		_passwordHasher = passwordHasher;
		_tokenGenerator = tokenGenerator;
		_outboxMessageQueue = outboxMessageQueue;
		_options = options;
	}

	public async Task<Result<Guid>> CreateUserAsync(
		string login,
		string fullName,
		string email,
		string password,
		CancellationToken cancellationToken
	)
	{
		var loginInUse = await _userRepository.ExistsByLoginAsync(
			login: login,
			cancellationToken: cancellationToken
		);

		if (loginInUse)
		{
			return Result<Guid>.Failure("Login já está em uso.");
		}

		var emailInUse = await _userRepository.ExistsByEmailAsync(
			email: email,
			cancellationToken: cancellationToken
		);

		if (emailInUse)
		{
			return Result<Guid>.Failure("E-mail já está em uso.");
		}

		var user = new User(
			login: login,
			fullName: fullName,
			email: email,
			passwordHash: _passwordHasher.Hash(password)
		);

		// Usuário, solicitação de e-mail e token de confirmação numa transação só: se qualquer parte
		// falhar, nada é gravado. Sem isso sobraria um usuário sem nenhum caminho pra confirmar o cadastro.
		return await _unitOfWork.ExecuteInTransactionAsync(
			operation: async transactionCancellationToken =>
			{
				_userRepository.Add(user);

				await RequestValidationEmailAsync(
					user: user,
					cancellationToken: transactionCancellationToken
				);

				await _unitOfWork.SaveChangesAsync(transactionCancellationToken);

				return Result<Guid>.Success(user.ExternalId);
			},
			cancellationToken: cancellationToken
		);
	}

	public async Task<Result> ConfirmEmailAsync(
		string token,
		CancellationToken cancellationToken
	)
	{
		var storedToken = await _tokenRepository.GetByHashAsync(
			tokenHash: _tokenGenerator.Hash(token),
			cancellationToken: cancellationToken
		);

		if (storedToken is null)
		{
			return Result.Failure("Token inválido.");
		}

		if (storedToken.Validated)
		{
			return Result.Failure("Token já foi utilizado.");
		}

		if (storedToken.ExpiresAt < DateTime.UtcNow)
		{
			return Result.Failure("Token expirado.");
		}

		if (storedToken.TokenType.Name != TokenTypeNames.UserCreationValidation)
		{
			return Result.Failure("Token inválido.");
		}

		storedToken.Validate();
		storedToken.User.ConfirmEmail();
		_tokenRepository.Update(storedToken);
		_userRepository.Update(storedToken.User);

		await _unitOfWork.SaveChangesAsync(cancellationToken);

		return Result.Success();
	}

	// O Auth decide por que, para quem e com quais dados o e-mail deve ser enviado. Ele não monta HTML,
	// não conhece SMTP e não espera pela entrega — quem entrega é o serviço de Notificações, depois,
	// a partir da mensagem publicada. Ver docs/0007.
	private async Task RequestValidationEmailAsync(
		User user,
		CancellationToken cancellationToken
	)
	{
		var tokenType = await _tokenTypeRepository.GetByNameAsync(
			name: TokenTypeNames.UserCreationValidation,
			cancellationToken: cancellationToken
		);

		var rawToken = _tokenGenerator.GenerateToken();

		// Um único instante de expiração para o token e para a solicitação: se fossem calculados em
		// dois lugares, a notificação poderia continuar sendo tentada depois de o link já ter morrido.
		var expiresAt = DateTime.UtcNow.AddHours(ValidationTokenExpirationHours);

		var confirmationUrl = $"{_options.PublicBaseUrl}/api/auth/confirm-email?token={Uri.EscapeDataString(rawToken)}";

		var notificationRequestId = _outboxMessageQueue.Add(
			messageType: NotificationMessageTypes.EmailRequested,
			schemaVersion: NotificationMessageTypes.EmailRequestedSchemaVersion,
			payload: new EmailNotificationRequestedV1(
				Recipient: user.Email,
				TemplateKey: EmailTemplateKeys.AuthEmailConfirmation,
				TemplateVersion: 1,
				Locale: EmailLocales.Default,
				Data: new Dictionary<string, string>
				{
					["FullName"] = user.FullName,
					["ConfirmationUrl"] = confirmationUrl
				},
				ExpiresAt: expiresAt
			)
		);

		_tokenRepository.Add(new Token(
			tokenType: tokenType,
			user: user,
			notificationRequestId: notificationRequestId,
			tokenHash: _tokenGenerator.Hash(rawToken),
			expiresAt: expiresAt
		));
	}
}
