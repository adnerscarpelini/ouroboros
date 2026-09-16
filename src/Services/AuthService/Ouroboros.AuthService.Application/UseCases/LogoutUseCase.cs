using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.AuthService.Application;

public sealed class LogoutUseCase : ILogoutUseCase
{
	private readonly IRefreshTokenRepository _refreshTokenRepository;
	private readonly IUnitOfWork _unitOfWork;
	private readonly ITokenGenerator _tokenGenerator;

	public LogoutUseCase(
		IRefreshTokenRepository refreshTokenRepository,
		IUnitOfWork unitOfWork,
		ITokenGenerator tokenGenerator
	)
	{
		_refreshTokenRepository = refreshTokenRepository;
		_unitOfWork = unitOfWork;
		_tokenGenerator = tokenGenerator;
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
		_refreshTokenRepository.Update(storedRefreshToken);

		await _unitOfWork.SaveChangesAsync(cancellationToken);

		return Result.Success();
	}
}
