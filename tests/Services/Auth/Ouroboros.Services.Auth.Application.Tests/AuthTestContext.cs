using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application.Tests;

// Monta os casos de uso da Application inteiramente com fakes em memória — sem EF Core, sem banco.
// É o que a separação de camadas comprou: a regra de negócio é testável sem infraestrutura nenhuma.
internal sealed class AuthTestContext
{
	public FakeUserRepository UserRepository { get; } = new();
	public FakeTokenRepository TokenRepository { get; } = new();
	public FakeRefreshTokenRepository RefreshTokenRepository { get; } = new();
	public FakeTokenTypeRepository TokenTypeRepository { get; } = new();
	public FakeUnitOfWork UnitOfWork { get; } = new();
	public FakePasswordHasher PasswordHasher { get; } = new();
	public FakeTokenGenerator TokenGenerator { get; } = new();
	public FakeEmailQueueService EmailQueueService { get; } = new();
	public FakeEmailTemplateRenderer EmailTemplateRenderer { get; } = new();
	public FakeJwtTokenGenerator JwtTokenGenerator { get; } = new();

	public AuthApplicationOptions Options { get; } = new(PublicBaseUrl: "http://localhost:5082");

	public UserRegistrationService CreateUserRegistrationService()
	{
		return new UserRegistrationService(
			userRepository: UserRepository,
			tokenRepository: TokenRepository,
			tokenTypeRepository: TokenTypeRepository,
			unitOfWork: UnitOfWork,
			passwordHasher: PasswordHasher,
			tokenGenerator: TokenGenerator,
			emailQueueService: EmailQueueService,
			emailTemplateRenderer: EmailTemplateRenderer,
			options: Options
		);
	}

	public AuthenticationService CreateAuthenticationService()
	{
		return new AuthenticationService(
			userRepository: UserRepository,
			refreshTokenRepository: RefreshTokenRepository,
			unitOfWork: UnitOfWork,
			passwordHasher: PasswordHasher,
			tokenGenerator: TokenGenerator,
			jwtTokenGenerator: JwtTokenGenerator
		);
	}

	public PasswordResetService CreatePasswordResetService()
	{
		return new PasswordResetService(
			userRepository: UserRepository,
			tokenRepository: TokenRepository,
			tokenTypeRepository: TokenTypeRepository,
			unitOfWork: UnitOfWork,
			passwordHasher: PasswordHasher,
			tokenGenerator: TokenGenerator,
			emailQueueService: EmailQueueService,
			emailTemplateRenderer: EmailTemplateRenderer,
			options: Options
		);
	}

	public User AddUser(
		string login = "jsilva",
		string email = "joao.silva@example.com",
		string password = "existing",
		bool confirmEmail = true
	)
	{
		var user = new User(
			login: login,
			fullName: "João Silva",
			email: email,
			passwordHash: PasswordHasher.Hash(password)
		);

		if (confirmEmail)
		{
			user.ConfirmEmail();
		}

		UserRepository.Add(user);

		return user;
	}

	public async Task<Token> AddTokenAsync(
		User user,
		string tokenTypeName,
		string tokenHash,
		DateTime expiresAt,
		bool validated = false
	)
	{
		var tokenType = await TokenTypeRepository.GetByNameAsync(
			name: tokenTypeName,
			cancellationToken: CancellationToken.None
		);

		var token = new Token(
			tokenType: tokenType,
			user: user,
			emailMessageId: 1,
			tokenHash: tokenHash,
			expiresAt: expiresAt
		);

		if (validated)
		{
			token.Validate();
		}

		TokenRepository.Add(token);

		return token;
	}

	public RefreshToken AddRefreshToken(
		User user,
		string tokenHash,
		DateTime expiresAt,
		bool revoked = false
	)
	{
		var refreshToken = new RefreshToken(
			user: user,
			tokenHash: tokenHash,
			expiresAt: expiresAt
		);

		if (revoked)
		{
			refreshToken.Revoke();
		}

		RefreshTokenRepository.Add(refreshToken);

		return refreshToken;
	}
}
