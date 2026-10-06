namespace Ouroboros.Auth.Application.UseCases.RefreshAccessToken;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Application.Settings;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;

public sealed class RefreshAccessTokenInteractor : IRefreshAccessTokenUseCase
{
    private const string BearerTokenType = "Bearer";

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly RefreshTokenSettings _refreshTokenSettings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLog _auditLog;

    public RefreshAccessTokenInteractor(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        ITokenGenerator tokenGenerator,
        RefreshTokenSettings refreshTokenSettings,
        IUnitOfWork unitOfWork,
        IAuditLog auditLog)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _tokenGenerator = tokenGenerator;
        _refreshTokenSettings = refreshTokenSettings;
        _unitOfWork = unitOfWork;
        _auditLog = auditLog;
    }

    public async Task<RefreshAccessTokenResponse> ExecuteAsync(RefreshAccessTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new InvalidRefreshTokenException("Refresh token is required");
        }

        var now = DateTimeOffset.UtcNow;
        var currentToken = await _refreshTokenRepository.GetByHashAsync(_tokenGenerator.Hash(request.RefreshToken));

        if (currentToken is null)
        {
            throw new InvalidRefreshTokenException("Invalid refresh token");
        }

        // Token ja revogado (rotacao, logout...) apresentado de novo: alguem usa uma copia (RFC 9700).
        if (currentToken.RevokedAt is not null)
        {
            await RevokeSessionAndRejectAsync(currentToken, now);
        }

        // Expirar nao e sinal de roubo: so rejeita, sem revogar a sessao.
        if (!currentToken.IsActive(now))
        {
            throw new InvalidRefreshTokenException("Refresh token has expired");
        }

        var user = await _userRepository.GetByExternalIdAsync(currentToken.UserExternalId);

        if (user is null)
        {
            throw new InvalidRefreshTokenException("Invalid refresh token");
        }

        if (!user.Active)
        {
            throw new DomainException("User is not active");
        }

        // O sucessor herda a sessao do token atual.
        var rawRefreshToken = _tokenGenerator.Generate();
        var newToken = RefreshToken.Create(
            user.ExternalId,
            _tokenGenerator.Hash(rawRefreshToken),
            now.Add(_refreshTokenSettings.Lifetime),
            currentToken.SessionId);

        // Revogar o atual e gravar o sucessor valem juntos ou nao valem: o par novo so sai depois do commit.
        var rotated = await _unitOfWork.ExecuteAsync(() => RotateAsync(currentToken, newToken, now));

        // Perdeu a disputa pelo token: outra requisicao o rotacionou antes. Sem janela de tolerancia, e reuso.
        if (!rotated)
        {
            await RevokeSessionAndRejectAsync(currentToken, now);
        }

        var accessToken = _jwtTokenGenerator.Generate(
            user.ExternalId,
            user.Login,
            user.Email,
            user.Role,
            currentToken.SessionId);

        return new RefreshAccessTokenResponse(
            BearerTokenType,
            accessToken.Value,
            accessToken.ExpiresAt,
            rawRefreshToken,
            newToken.ExpiresAt);
    }

    private async Task<bool> RotateAsync(
        RefreshToken currentToken,
        RefreshToken newToken,
        DateTimeOffset now)
    {
        currentToken.Revoke(now);

        // Condicional no SQL (nao revogado e nao expirado): so uma requisicao consegue rotacionar o mesmo token.
        if (!await _refreshTokenRepository.TryRevokeAsync(currentToken))
        {
            return false;
        }

        await _refreshTokenRepository.AddAsync(newToken);

        return true;
    }

    // Roda fora da transacao da rotacao, senao o rollback desfaria a revogacao. So a sessao do token e afetada.
    private async Task RevokeSessionAndRejectAsync(
        RefreshToken token,
        DateTimeOffset now)
    {
        await _refreshTokenRepository.RevokeAllActiveBySessionAsync(token.SessionId, now);

        // Fora de qualquer transacao (autocommit): o evento de falha sobrevive ao rollback da rotacao.
        await _auditLog.RecordAsync(new AuditEvent(
            AuditEventType.RefreshTokenReuseDetected,
            AuditOutcome.Failure,
            token.UserExternalId,
            SessionId: token.SessionId,
            Reason: AuditReason.ReuseDetected));

        throw new RefreshTokenReuseException(token.UserExternalId, token.SessionId);
    }
}
