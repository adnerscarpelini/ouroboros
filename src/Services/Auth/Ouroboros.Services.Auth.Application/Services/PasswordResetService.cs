using Ouroboros.BuildingBlocks.Application;
using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application;

public sealed class PasswordResetService : IPasswordResetService
{
	private const int PasswordResetTokenExpirationHours = 1;

	private readonly IUserRepository _userRepository;
	private readonly ITokenRepository _tokenRepository;
	private readonly ITokenTypeRepository _tokenTypeRepository;
	private readonly IUnitOfWork _unitOfWork;
	private readonly IPasswordHasher _passwordHasher;
	private readonly ITokenGenerator _tokenGenerator;
	private readonly IEmailQueueService _emailQueueService;
	private readonly IEmailTemplateRenderer _emailTemplateRenderer;
	private readonly AuthApplicationOptions _options;

	public PasswordResetService(
		IUserRepository userRepository,
		ITokenRepository tokenRepository,
		ITokenTypeRepository tokenTypeRepository,
		IUnitOfWork unitOfWork,
		IPasswordHasher passwordHasher,
		ITokenGenerator tokenGenerator,
		IEmailQueueService emailQueueService,
		IEmailTemplateRenderer emailTemplateRenderer,
		AuthApplicationOptions options
	)
	{
		_userRepository = userRepository;
		_tokenRepository = tokenRepository;
		_tokenTypeRepository = tokenTypeRepository;
		_unitOfWork = unitOfWork;
		_passwordHasher = passwordHasher;
		_tokenGenerator = tokenGenerator;
		_emailQueueService = emailQueueService;
		_emailTemplateRenderer = emailTemplateRenderer;
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

				await EnqueuePasswordResetEmailAsync(
					user: user,
					cancellationToken: transactionCancellationToken
				);

				await _unitOfWork.SaveChangesAsync(transactionCancellationToken);
			},
			cancellationToken: cancellationToken
		);
	}

	public async Task<Result> ResetPasswordAsync(
		string token,
		string newPassword,
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

		if (storedToken.TokenType.Name != TokenTypeNames.PasswordReset)
		{
			return Result.Failure("Token inválido.");
		}

		storedToken.Validate();
		storedToken.User.ResetPassword(_passwordHasher.Hash(newPassword));

		await _unitOfWork.SaveChangesAsync(cancellationToken);

		return Result.Success();
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
		}
	}

	private async Task EnqueuePasswordResetEmailAsync(
		User user,
		CancellationToken cancellationToken
	)
	{
		var tokenType = await _tokenTypeRepository.GetByNameAsync(
			name: TokenTypeNames.PasswordReset,
			cancellationToken: cancellationToken
		);

		var rawToken = _tokenGenerator.GenerateToken();

		// Sem página própria ainda: aponta pro front-end que vai coletar a nova senha
		// e chamar POST /api/auth/reset-password com token + senha.
		var resetUrl = $"{_options.PublicBaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

		var bodyHtml = await _emailTemplateRenderer.RenderAsync(
			templateName: EmailTemplateNames.PasswordReset,
			placeholders: new Dictionary<string, string>
			{
				["FullName"] = user.FullName,
				["ResetUrl"] = resetUrl
			},
			cancellationToken: cancellationToken
		);

		var emailMessageId = await _emailQueueService.EnqueueAsync(
			subject: "Redefinição de senha",
			bodyHtml: bodyHtml,
			recipient: user.Email,
			cancellationToken: cancellationToken
		);

		_tokenRepository.Add(new Token(
			tokenType: tokenType,
			user: user,
			emailMessageId: emailMessageId,
			tokenHash: _tokenGenerator.Hash(rawToken),
			expiresAt: DateTime.UtcNow.AddHours(PasswordResetTokenExpirationHours)
		));
	}
}
