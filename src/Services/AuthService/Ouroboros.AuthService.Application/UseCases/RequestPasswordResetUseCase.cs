using Ouroboros.BuildingBlocks.Application;
using Ouroboros.Contracts.Notifications;
using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Application;

public sealed class RequestPasswordResetUseCase : IRequestPasswordResetUseCase
{
	private const int PasswordResetTokenExpirationHours = 1;

	private readonly IUserRepository _userRepository;
	private readonly ITokenRepository _tokenRepository;
	private readonly ITokenTypeRepository _tokenTypeRepository;
	private readonly IUnitOfWork _unitOfWork;
	private readonly ITokenGenerator _tokenGenerator;
	private readonly IOutboxMessageQueue _outboxMessageQueue;
	private readonly AuthApplicationOptions _options;

	public RequestPasswordResetUseCase(
		IUserRepository userRepository,
		ITokenRepository tokenRepository,
		ITokenTypeRepository tokenTypeRepository,
		IUnitOfWork unitOfWork,
		ITokenGenerator tokenGenerator,
		IOutboxMessageQueue outboxMessageQueue,
		AuthApplicationOptions options
	)
	{
		_userRepository = userRepository;
		_tokenRepository = tokenRepository;
		_tokenTypeRepository = tokenTypeRepository;
		_unitOfWork = unitOfWork;
		_tokenGenerator = tokenGenerator;
		_outboxMessageQueue = outboxMessageQueue;
		_options = options;
	}

	public async Task RequestPasswordResetAsync(
		string email,
		CancellationToken cancellationToken
	)
	{
		var user = await _userRepository.GetByEmailAsync(
			email: email,
			cancellationToken: cancellationToken
		);

		// Sempre silencioso, mesmo se o e-mail não existir — evita enumeração de contas.
		if (user is null)
		{
			return;
		}

		// Invalidar os tokens antigos e criar o novo precisam acontecer juntos: se só a primeira parte
		// fosse gravada, o usuário ficaria sem nenhum token válido para redefinir a senha.
		await _unitOfWork.ExecuteInTransactionAsync(
			operation: async transactionCancellationToken =>
			{
				await InvalidatePendingPasswordResetTokensAsync(
					user: user,
					cancellationToken: transactionCancellationToken
				);

				await RequestPasswordResetEmailAsync(
					user: user,
					cancellationToken: transactionCancellationToken
				);

				await _unitOfWork.SaveChangesAsync(transactionCancellationToken);
			},
			cancellationToken: cancellationToken
		);
	}

	private async Task InvalidatePendingPasswordResetTokensAsync(
		User user,
		CancellationToken cancellationToken
	)
	{
		var pendingTokens = await _tokenRepository.GetPendingByUserAndTypeAsync(
			user: user,
			tokenTypeName: TokenTypeNames.PasswordReset,
			cancellationToken: cancellationToken
		);

		foreach (var pendingToken in pendingTokens)
		{
			// Reaproveita Validate() pra invalidar: um token de reset não usado
			// perde a validade assim que um pedido de reset mais novo é feito.
			pendingToken.Validate();
			_tokenRepository.Update(pendingToken);
		}
	}

	private async Task RequestPasswordResetEmailAsync(
		User user,
		CancellationToken cancellationToken
	)
	{
		var tokenType = await _tokenTypeRepository.GetByNameAsync(
			name: TokenTypeNames.PasswordReset,
			cancellationToken: cancellationToken
		);

		var rawToken = _tokenGenerator.GenerateToken();

		var expiresAt = DateTime.UtcNow.AddHours(PasswordResetTokenExpirationHours);

		// Sem página própria ainda: aponta pro front-end que vai coletar a nova senha
		// e chamar POST /api/auth/reset-password com token + senha.
		var resetUrl = $"{_options.PublicBaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

		var notificationRequestId = _outboxMessageQueue.Add(
			messageType: NotificationMessageTypes.EmailRequested,
			schemaVersion: NotificationMessageTypes.EmailRequestedSchemaVersion,
			payload: new EmailNotificationRequestedV1(
				Recipient: user.Email,
				TemplateKey: EmailTemplateKeys.AuthPasswordReset,
				TemplateVersion: 1,
				Locale: EmailLocales.Default,
				Data: new Dictionary<string, string>
				{
					["FullName"] = user.FullName,
					["ResetUrl"] = resetUrl
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
