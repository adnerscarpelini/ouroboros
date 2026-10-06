namespace Ouroboros.Auth.Application.UseCases.LogoutAll;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;

public sealed class LogoutAllInteractor : ILogoutAllUseCase
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLog _auditLog;

    public LogoutAllInteractor(
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IAuditLog auditLog)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _auditLog = auditLog;
    }

    // Encerra todas as sessoes do usuario. Os access tokens ja emitidos valem ate expirar (no maximo 15 min).
    public async Task<LogoutAllResponse> ExecuteAsync(LogoutAllRequest request)
    {
        // A revogacao e o evento de auditoria valem juntos ou nao valem.
        await _unitOfWork.ExecuteAsync(async () =>
        {
            await _refreshTokenRepository.RevokeAllActiveByUserAsync(request.UserId, DateTimeOffset.UtcNow);

            await _auditLog.RecordAsync(new AuditEvent(AuditEventType.LogoutAll, AuditOutcome.Success, request.UserId));
        });

        return new LogoutAllResponse(request.UserId);
    }
}
