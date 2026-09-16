using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Application;

// Colaborador interno compartilhado por ILoginUseCase e IRefreshTokenUseCase: os dois emitem o mesmo
// par access+refresh token ao final, e essa emissão não é, em si, um caso de uso disparado pela Api.
public sealed class AuthenticationResultFactory
{
	private const int RefreshTokenExpirationDays = 30;

	private readonly IRefreshTokenRepository _refreshTokenRepository;
	private readonly ITokenGenerator _tokenGenerator;
	private readonly IJwtTokenGenerator _jwtTokenGenerator;

	public AuthenticationResultFactory(
		IRefreshTokenRepository refreshTokenRepository,
		ITokenGenerator tokenGenerator,
		IJwtTokenGenerator jwtTokenGenerator
	)
	{
		_refreshTokenRepository = refreshTokenRepository;
		_tokenGenerator = tokenGenerator;
		_jwtTokenGenerator = jwtTokenGenerator;
	}

	public AuthenticationResult IssueFor(User user)
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
