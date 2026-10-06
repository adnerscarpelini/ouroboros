namespace Ouroboros.Auth.Application.UseCases.LogoutAll;

using Ouroboros.Auth.Application.Gateways;

public sealed class LogoutAllInteractor : ILogoutAllUseCase
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public LogoutAllInteractor(IRefreshTokenRepository refreshTokenRepository)
    {
        _refreshTokenRepository = refreshTokenRepository;
    }

    // Encerra todas as sessoes do usuario. Os access tokens ja emitidos valem ate expirar (no maximo 15 min).
    public async Task<LogoutAllResponse> ExecuteAsync(LogoutAllRequest request)
    {
        await _refreshTokenRepository.RevokeAllActiveByUserAsync(request.UserId, DateTimeOffset.UtcNow);

        return new LogoutAllResponse(request.UserId);
    }
}
