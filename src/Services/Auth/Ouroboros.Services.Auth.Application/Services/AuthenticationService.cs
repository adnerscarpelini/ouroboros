using Ouroboros.BuildingBlocks.Application;
using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application;

public sealed class AuthenticationService : IAuthenticationService
{
	private const int RefreshTokenExpirationDays = 30;

	private readonly IUserRepository _userRepository;
	private readonly IRefreshTokenRepository _refreshTokenRepository;
	private readonly IUnitOfWork _unitOfWork;
	private readonly IPasswordHasher _passwordHasher;
	private readonly ITokenGenerator _tokenGenerator;
	private readonly IJwtTokenGenerator _jwtTokenGenerator;

	public AuthenticationService(
		IUserRepository userRepository,
		IRefreshTokenRepository refreshTokenRepository,
		IUnitOfWork unitOfWork,
		IPasswordHasher passwordHasher,
		ITokenGenerator tokenGenerator,
		IJwtTokenGenerator jwtTokenGenerator
	)
	{
		_userRepository = userRepository;
		_refreshTokenRepository = refreshTokenRepository;
		_unitOfWork = unitOfWork;
		_passwordHasher = passwordHasher;
		_tokenGenerator = tokenGenerator;
		_jwtTokenGenerator = jwtTokenGenerator;
	}

	public async Task<Result<AuthenticationResult>> LoginAsync(
		string login,
		string password,
		CancellationToken cancellationToken
	)
	{
		var user = await _userRepository.GetByLoginAsync(
			login: login,
			cancellationToken: cancellationToken
		);

		if (user is null)
		{
			return Result<AuthenticationResult>.Failure("Login ou senha inválidos.");
		}

		if (user.IsLockedOut())
		{
			return Result<AuthenticationResult>.Failure("Conta temporariamente bloqueada por excesso de tentativas. Tente novamente mais tarde.");
		}

		if (!_passwordHasher.Verify(passwordHash: user.PasswordHash, password: password))
		{
			user.RegisterFailedLoginAttempt();

			await _unitOfWork.SaveChangesAsync(cancellationToken);

			return Result<AuthenticationResult>.Failure("Login ou senha inválidos.");
		}

		if (!user.IsActive)
		{
			return Result<AuthenticationResult>.Failure("Confirme seu e-mail antes de fazer login.");
		}

		user.RegisterSuccessfulLogin();

		var authenticationResult = IssueAuthenticationResult(user);

		await _unitOfWork.SaveChangesAsync(cancellationToken);

		return Result<AuthenticationResult>.Success(authenticationResult);
	}

	public async Task<Result<AuthenticationResult>> RefreshTokenAsync(
		string refreshToken,
		CancellationToken cancellationToken
	)
	{
		var storedRefreshToken = await _refreshTokenRepository.GetByHashAsync(
			tokenHash: _tokenGenerator.Hash(refreshToken),
			cancellationToken: cancellationToken
		);

		if (storedRefreshToken is null || storedRefreshToken.RevokedAt.HasValue)
		{
			return Result<AuthenticationResult>.Failure("Token inválido.");
		}

		if (storedRefreshToken.ExpiresAt < DateTime.UtcNow)
		{
			return Result<AuthenticationResult>.Failure("Token expirado.");
		}

		// Rotação: o refresh token usado é revogado e um novo par access+refresh é emitido.
		storedRefreshToken.Revoke();

		var authenticationResult = IssueAuthenticationResult(storedRefreshToken.User);

		await _unitOfWork.SaveChangesAsync(cancellationToken);

		return Result<AuthenticationResult>.Success(authenticationResult);
	}

	public async Task<Result> LogoutAsync(
		string refreshToken,
		CancellationToken cancellationToken
	)
	{
		var storedRefreshToken = await _refreshTokenRepository.GetByHashAsync(
			tokenHash: _tokenGenerator.Hash(refreshToken),
			cancellationToken: cancellationToken
		);

		if (storedRefreshToken is null || storedRefreshToken.RevokedAt.HasValue)
		{
			return Result.Failure("Token inválido.");
		}

		storedRefreshToken.Revoke();

		await _unitOfWork.SaveChangesAsync(cancellationToken);

		return Result.Success();
	}

	private AuthenticationResult IssueAuthenticationResult(User user)
	{
		var accessTokenResult = _jwtTokenGenerator.GenerateToken(user);

		var rawRefreshToken = _tokenGenerator.GenerateToken();
		var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays);

		_refreshTokenRepository.Add(new RefreshToken(
			user: user,
			tokenHash: _tokenGenerator.Hash(rawRefreshToken),
			expiresAt: refreshTokenExpiresAt
		));

		return new AuthenticationResult(
			AccessToken: accessTokenResult.AccessToken,
			ExpiresAt: accessTokenResult.ExpiresAt,
			RefreshToken: rawRefreshToken,
			RefreshTokenExpiresAt: refreshTokenExpiresAt
		);
	}
}
