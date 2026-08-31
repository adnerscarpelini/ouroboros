using Microsoft.EntityFrameworkCore;
using Ouroboros.Common.Application;
using Ouroboros.Modules.Auth.Application;
using Ouroboros.Modules.Auth.Domain;

namespace Ouroboros.Modules.Auth.Infrastructure;

public sealed class UserService : IUserService
{
	private const int ValidationTokenExpirationHours = 24;

	private readonly AuthDbContext _dbContext;
	private readonly IPasswordHasher _passwordHasher;
	private readonly ITokenGenerator _tokenGenerator;
	private readonly IEmailQueueService _emailQueueService;

	public UserService(
		AuthDbContext dbContext,
		IPasswordHasher passwordHasher,
		ITokenGenerator tokenGenerator,
		IEmailQueueService emailQueueService
	)
	{
		_dbContext = dbContext;
		_passwordHasher = passwordHasher;
		_tokenGenerator = tokenGenerator;
		_emailQueueService = emailQueueService;
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

		var bodyHtml = $"<p>Olá, {user.FullName}!</p><p>Use o token abaixo para confirmar seu cadastro:</p><p><code>{rawToken}</code></p>";

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
