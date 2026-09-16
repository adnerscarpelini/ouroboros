using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Application.Tests;

// Monta os casos de uso da Application inteiramente com fakes em memória — sem banco.
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
	public FakeOutboxMessageQueue OutboxMessageQueue { get; } = new();
	public FakeJwtTokenGenerator JwtTokenGenerator { get; } = new();

	public AuthApplicationOptions Options { get; } = new(PublicBaseUrl: "http://localhost:5082");

	public AuthenticationResultFactory CreateAuthenticationResultFactory()
	{
		return new AuthenticationResultFactory(
			refreshTokenRepository: RefreshTokenRepository,
			tokenGenerator: TokenGenerator,
			jwtTokenGenerator: JwtTokenGenerator
		);
	}

	public RegisterUserUseCase CreateRegisterUserUseCase()
	{
		return new RegisterUserUseCase(
			userRepository: UserRepository,
			tokenRepository: TokenRepository,
			tokenTypeRepository: TokenTypeRepository,
			unitOfWork: UnitOfWork,
			passwordHasher: PasswordHasher,
			tokenGenerator: TokenGenerator,
			outboxMessageQueue: OutboxMessageQueue,
			options: Options
		);
	}

	public ConfirmEmailUseCase CreateConfirmEmailUseCase()
	{
		return new ConfirmEmailUseCase(
			userRepository: UserRepository,
			tokenRepository: TokenRepository,
			unitOfWork: UnitOfWork,
			tokenGenerator: TokenGenerator
		);
	}

	public LoginUseCase CreateLoginUseCase()
	{
		return new LoginUseCase(
			userRepository: UserRepository,
			unitOfWork: UnitOfWork,
			passwordHasher: PasswordHasher,
			authenticationResultFactory: CreateAuthenticationResultFactory()
		);
	}

	public RefreshTokenUseCase CreateRefreshTokenUseCase()
	{
		return new RefreshTokenUseCase(
			refreshTokenRepository: RefreshTokenRepository,
			unitOfWork: UnitOfWork,
			tokenGenerator: TokenGenerator,
			authenticationResultFactory: CreateAuthenticationResultFactory()
		);
	}

	public LogoutUseCase CreateLogoutUseCase()
	{
		return new LogoutUseCase(
			refreshTokenRepository: RefreshTokenRepository,
			unitOfWork: UnitOfWork,
			tokenGenerator: TokenGenerator
		);
	}

	public RequestPasswordResetUseCase CreateRequestPasswordResetUseCase()
	{
		return new RequestPasswordResetUseCase(
			userRepository: UserRepository,
			tokenRepository: TokenRepository,
			tokenTypeRepository: TokenTypeRepository,
			unitOfWork: UnitOfWork,
			tokenGenerator: TokenGenerator,
			outboxMessageQueue: OutboxMessageQueue,
			options: Options
		);
	}

	public ResetPasswordUseCase CreateResetPasswordUseCase()
	{
		return new ResetPasswordUseCase(
			userRepository: UserRepository,
			tokenRepository: TokenRepository,
			unitOfWork: UnitOfWork,
			passwordHasher: PasswordHasher,
			tokenGenerator: TokenGenerator
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
			notificationRequestId: Guid.NewGuid(),
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
