namespace Ouroboros.Auth.Application.UseCases.Logout;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;

public sealed class LogoutInteractor : ILogoutUseCase
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLog _auditLog;

    public LogoutInteractor(
        IRefreshTokenRepository refreshTokenRepository,
        ITokenGenerator tokenGenerator,
        IUnitOfWork unitOfWork,
        IAuditLog auditLog)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _tokenGenerator = tokenGenerator;
        _unitOfWork = unitOfWork;
        _auditLog = auditLog;
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

        // Logout e idempotente: token ja revogado ou expirado nao tem mais sessao pra encerrar, e nao gera evento.
        if (!refreshToken.IsActive(now))
        {
            return new LogoutResponse(false);
        }

        refreshToken.Revoke(now);

        // A revogacao e o evento de auditoria valem juntos ou nao valem.
        var revoked = await _unitOfWork.ExecuteAsync(() => RevokeAsync(refreshToken));

        return new LogoutResponse(revoked);
    }

    private async Task<bool> RevokeAsync(RefreshToken refreshToken)
    {
        if (!await _refreshTokenRepository.TryRevokeAsync(refreshToken))
        {
            return false;
        }

        await _auditLog.RecordAsync(new AuditEvent(
            AuditEventType.Logout,
            AuditOutcome.Success,
            refreshToken.UserExternalId,
            SessionId: refreshToken.SessionId));

        return true;
    }
}
