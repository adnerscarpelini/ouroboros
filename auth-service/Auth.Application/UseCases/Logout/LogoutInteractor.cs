namespace Ouroboros.Auth.Application.UseCases.Logout;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Exceptions;

public sealed class LogoutInteractor : ILogoutUseCase
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenGenerator _tokenGenerator;

    public LogoutInteractor(
        IRefreshTokenRepository refreshTokenRepository,
        ITokenGenerator tokenGenerator)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<LogoutResponse> ExecuteAsync(LogoutRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new InvalidRefreshTokenException("Refresh token is required");
        }

        var refreshToken = await _refreshTokenRepository.GetByHashAsync(_tokenGenerator.Hash(request.RefreshToken));

        if (refreshToken is null)
        {
            throw new InvalidRefreshTokenException("Invalid refresh token");
        }

        var now = DateTimeOffset.UtcNow;

        // Logout e idempotente: token ja revogado ou expirado nao tem mais sessao pra encerrar.
        if (!refreshToken.IsActive(now))
        {
            return new LogoutResponse(false);
        }

        refreshToken.Revoke(now);

        var revoked = await _refreshTokenRepository.TryRevokeAsync(refreshToken);

        return new LogoutResponse(revoked);
    }
}
