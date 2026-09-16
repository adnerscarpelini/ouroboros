using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.AuthService.Application;

public sealed class RefreshTokenUseCase : IRefreshTokenUseCase
{
	private readonly IRefreshTokenRepository _refreshTokenRepository;
	private readonly IUnitOfWork _unitOfWork;
	private readonly ITokenGenerator _tokenGenerator;
	private readonly AuthenticationResultFactory _authenticationResultFactory;

	public RefreshTokenUseCase(
		IRefreshTokenRepository refreshTokenRepository,
		IUnitOfWork unitOfWork,
		ITokenGenerator tokenGenerator,
		AuthenticationResultFactory authenticationResultFactory
	)
	{
		_refreshTokenRepository = refreshTokenRepository;
		_unitOfWork = unitOfWork;
		_tokenGenerator = tokenGenerator;
		_authenticationResultFactory = authenticationResultFactory;
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
		_refreshTokenRepository.Update(storedRefreshToken);

		var authenticationResult = _authenticationResultFactory.IssueFor(storedRefreshToken.User);

		await _unitOfWork.SaveChangesAsync(cancellationToken);

		return Result<AuthenticationResult>.Success(authenticationResult);
	}
}
