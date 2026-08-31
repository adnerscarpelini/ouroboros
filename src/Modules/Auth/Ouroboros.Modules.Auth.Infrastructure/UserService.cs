using Microsoft.EntityFrameworkCore;
using Ouroboros.Common.Application;
using Ouroboros.Modules.Auth.Application;
using Ouroboros.Modules.Auth.Domain;

namespace Ouroboros.Modules.Auth.Infrastructure;

public sealed class UserService : IUserService
{
	private const int ValidationTokenExpirationHours = 24;
	private const int PasswordResetTokenExpirationHours = 1;

	private readonly AuthDbContext _dbContext;
	private readonly IPasswordHasher _passwordHasher;
	private readonly ITokenGenerator _tokenGenerator;
	private readonly IEmailQueueService _emailQueueService;
	private readonly IJwtTokenGenerator _jwtTokenGenerator;
	private readonly AuthOptions _authOptions;

	public UserService(
		AuthDbContext dbContext,
		IPasswordHasher passwordHasher,
		ITokenGenerator tokenGenerator,
		IEmailQueueService emailQueueService,
		IJwtTokenGenerator jwtTokenGenerator,
		AuthOptions authOptions
	)
	{
		_dbContext = dbContext;
		_passwordHasher = passwordHasher;
		_tokenGenerator = tokenGenerator;
		_emailQueueService = emailQueueService;
		_jwtTokenGenerator = jwtTokenGenerator;
		_authOptions = authOptions;
	}

	public async Task<Result<Guid>> CreateUserAsync(
		string login,
		string fullName,
		string email,
		string password,
		CancellationToken cancellationToken
	)
	{
		var loginInUse = await _dbContext.Users.AnyAsync(u => u.Login == login, cancellationToken);

		if (loginInUse)
		{
			return Result<Guid>.Failure("Login já está em uso.");
		}

		var emailInUse = await _dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken);

		if (emailInUse)
		{
			return Result<Guid>.Failure("E-mail já está em uso.");
		}

		var passwordHash = _passwordHasher.Hash(password);

		var user = new User(
			login: login,
			fullName: fullName,
			email: email,
			passwordHash: passwordHash
		);

		_dbContext.Users.Add(user);

		await _dbContext.SaveChangesAsync(cancellationToken);

		await EnqueueValidationEmailAsync(user, cancellationToken);

		return Result<Guid>.Success(user.ExternalId);
	}

	public async Task<Result> ConfirmEmailAsync(
		string token,
		CancellationToken cancellationToken
	)
	{
		var tokenHash = _tokenGenerator.Hash(token);

		var storedToken = await _dbContext.Tokens
			.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

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

		var tokenType = await _dbContext.TokenTypes.SingleAsync(t => t.Id == storedToken.TokenTypeId, cancellationToken);

		if (tokenType.Name != TokenTypeNames.UserCreationValidation)
		{
			return Result.Failure("Token inválido.");
		}

		var user = await _dbContext.Users.SingleAsync(u => u.Id == storedToken.UserId, cancellationToken);

		storedToken.Validate();
		user.ConfirmEmail();

		await _dbContext.SaveChangesAsync(cancellationToken);

		return Result.Success();
	}

	public async Task<Result<AuthenticationResult>> LoginAsync(
		string login,
		string password,
		CancellationToken cancellationToken
	)
	{
		var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Login == login, cancellationToken);

		if (user is null)
		{
			return Result<AuthenticationResult>.Failure("Login ou senha inválidos.");
		}

		if (user.IsLockedOut())
		{
			return Result<AuthenticationResult>.Failure("Conta temporariamente bloqueada por excesso de tentativas. Tente novamente mais tarde.");
		}

		if (!_passwordHasher.Verify(user.PasswordHash, password))
		{
			user.RegisterFailedLoginAttempt();

			await _dbContext.SaveChangesAsync(cancellationToken);

			return Result<AuthenticationResult>.Failure("Login ou senha inválidos.");
		}

		if (!user.IsActive)
		{
			return Result<AuthenticationResult>.Failure("Confirme seu e-mail antes de fazer login.");
		}

		user.RegisterSuccessfulLogin();

		await _dbContext.SaveChangesAsync(cancellationToken);

		var authenticationResult = _jwtTokenGenerator.GenerateToken(user);

		return Result<AuthenticationResult>.Success(authenticationResult);
	}

	public async Task RequestPasswordResetAsync(
		string email,
		CancellationToken cancellationToken
	)
	{
		var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

		// Sempre silencioso, mesmo se o e-mail não existir — evita enumeração de contas.
		if (user is null)
		{
			return;
		}

		await InvalidatePendingPasswordResetTokensAsync(user.Id, cancellationToken);
		await EnqueuePasswordResetEmailAsync(user, cancellationToken);
	}

	public async Task<Result> ResetPasswordAsync(
		string token,
		string newPassword,
		CancellationToken cancellationToken
	)
	{
		var tokenHash = _tokenGenerator.Hash(token);

		var storedToken = await _dbContext.Tokens
			.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

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

		var tokenType = await _dbContext.TokenTypes.SingleAsync(t => t.Id == storedToken.TokenTypeId, cancellationToken);

		if (tokenType.Name != TokenTypeNames.PasswordReset)
		{
			return Result.Failure("Token inválido.");
		}

		var user = await _dbContext.Users.SingleAsync(u => u.Id == storedToken.UserId, cancellationToken);

		var newPasswordHash = _passwordHasher.Hash(newPassword);

		storedToken.Validate();
		user.ResetPassword(newPasswordHash);

		await _dbContext.SaveChangesAsync(cancellationToken);

		return Result.Success();
	}

	private async Task InvalidatePendingPasswordResetTokensAsync(
		long userId,
		CancellationToken cancellationToken
	)
	{
		var tokenTypeId = await _dbContext.TokenTypes
			.Where(t => t.Name == TokenTypeNames.PasswordReset)
			.Select(t => t.Id)
			.SingleAsync(cancellationToken);

		var pendingTokens = await _dbContext.Tokens
			.Where(t => t.UserId == userId && t.TokenTypeId == tokenTypeId && !t.Validated)
			.ToListAsync(cancellationToken);

		foreach (var pendingToken in pendingTokens)
		{
			// Reaproveita Validate() pra invalidar: um token de reset não usado
			// perde a validade assim que um pedido de reset mais novo é feito.
			pendingToken.Validate();
		}

		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	private async Task EnqueuePasswordResetEmailAsync(
		User user,
		CancellationToken cancellationToken
	)
	{
		var tokenTypeId = await _dbContext.TokenTypes
			.Where(t => t.Name == TokenTypeNames.PasswordReset)
			.Select(t => t.Id)
			.SingleAsync(cancellationToken);

		var rawToken = _tokenGenerator.GenerateToken();
		var tokenHash = _tokenGenerator.Hash(rawToken);

		// Sem página própria ainda: aponta pro front-end que vai coletar a nova senha
		// e chamar POST /api/auth/reset-password com token + senha.
		var resetUrl = $"{_authOptions.ApiBaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

		var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "PasswordResetEmail.html");
		var bodyHtml = (await File.ReadAllTextAsync(templatePath, cancellationToken))
			.Replace("{{FullName}}", user.FullName)
			.Replace("{{ResetUrl}}", resetUrl);

		var emailMessageId = await _emailQueueService.EnqueueAsync(
			subject: "Redefinição de senha",
			bodyHtml: bodyHtml,
			recipient: user.Email,
			cancellationToken: cancellationToken
		);

		var token = new Token(
			tokenTypeId: tokenTypeId,
			userId: user.Id,
			emailMessageId: emailMessageId,
			tokenHash: tokenHash,
			expiresAt: DateTime.UtcNow.AddHours(PasswordResetTokenExpirationHours)
		);

		_dbContext.Tokens.Add(token);

		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	private async Task EnqueueValidationEmailAsync(
		User user,
		CancellationToken cancellationToken
	)
	{
		var tokenTypeId = await _dbContext.TokenTypes
			.Where(t => t.Name == TokenTypeNames.UserCreationValidation)
			.Select(t => t.Id)
			.SingleAsync(cancellationToken);

		var rawToken = _tokenGenerator.GenerateToken();
		var tokenHash = _tokenGenerator.Hash(rawToken);

		var confirmationUrl = $"{_authOptions.ApiBaseUrl}/api/auth/confirm-email?token={Uri.EscapeDataString(rawToken)}";

		var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "UserCreationValidationEmail.html");
		var bodyHtml = (await File.ReadAllTextAsync(templatePath, cancellationToken))
			.Replace("{{FullName}}", user.FullName)
			.Replace("{{ConfirmationUrl}}", confirmationUrl);

		var emailMessageId = await _emailQueueService.EnqueueAsync(
			subject: "Confirme seu cadastro",
			bodyHtml: bodyHtml,
			recipient: user.Email,
			cancellationToken: cancellationToken
		);

		var token = new Token(
			tokenTypeId: tokenTypeId,
			userId: user.Id,
			emailMessageId: emailMessageId,
			tokenHash: tokenHash,
			expiresAt: DateTime.UtcNow.AddHours(ValidationTokenExpirationHours)
		);

		_dbContext.Tokens.Add(token);

		await _dbContext.SaveChangesAsync(cancellationToken);
	}
}
