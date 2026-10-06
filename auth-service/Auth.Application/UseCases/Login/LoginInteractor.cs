namespace Ouroboros.Auth.Application.UseCases.Login;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Application.Settings;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Ouroboros.Auth.Domain.Policies;

public sealed class LoginInteractor : ILoginUseCase
{
    private const string BearerTokenType = "Bearer";

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly RefreshTokenSettings _refreshTokenSettings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLog _auditLog;

    public LoginInteractor(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ITokenGenerator tokenGenerator,
        RefreshTokenSettings refreshTokenSettings,
        IUnitOfWork unitOfWork,
        IAuditLog auditLog)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _tokenGenerator = tokenGenerator;
        _refreshTokenSettings = refreshTokenSettings;
        _unitOfWork = unitOfWork;
        _auditLog = auditLog;
    }

    public async Task<LoginResponse> ExecuteAsync(LoginRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var user = string.IsNullOrWhiteSpace(request.Login)
            ? null
            : await _userRepository.GetByLoginAsync(IdentityPolicy.Normalize(request.Login));
        var canVerifyRealHash = user is not null && !user.IsLockedOut(now);
        var hash = canVerifyRealHash ? user!.PasswordHash : _passwordHasher.DummyHash;
        var passwordMatches = _passwordHasher.Verify(request.Password ?? string.Empty, hash);

        if (!canVerifyRealHash || !passwordMatches)
        {
            await RejectAsync(user, canVerifyRealHash, now);
        }

        if (!user!.Active)
        {
            await RecordFailureAsync(user.ExternalId, AuditReason.InactiveAccount);

            throw new DomainException("User is not active");
        }

        // Varias sessoes simultaneas (spec 2026092506): o login nao revoga as anteriores, cada dispositivo tem a sua.
        var sessionId = Guid.NewGuid();
        var rawRefreshToken = _tokenGenerator.Generate();
        var refreshToken = RefreshToken.Create(
            user.ExternalId,
            _tokenGenerator.Hash(rawRefreshToken),
            now.Add(_refreshTokenSettings.Lifetime),
            sessionId);

        // Hash gerado com custo menor que o atual e refeito agora, que a senha em texto puro esta em maos. Calculado fora
        // da transacao (e lento de proposito). O UPDATE so age se o hash ainda for o lido: uma troca de senha em paralelo vence.
        var rehashedPassword = _passwordHasher.NeedsRehash(user.PasswordHash)
            ? _passwordHasher.Hash(request.Password!)
            : null;

        // Registro do login, re-hash, refresh token e evento de auditoria valem juntos ou nao valem. Os tokens so saem
        // depois do commit.
        try
        {
            await _unitOfWork.ExecuteAsync(() => IssueSessionAsync(user, refreshToken, rehashedPassword, now));
        }
        catch (InvalidCredentialsException)
        {
            // Conta bloqueada entre a leitura e a escrita: a transacao foi desfeita, o evento de falha vai fora dela.
            await RecordFailureAsync(user.ExternalId, AuditReason.LockedOut);

            throw;
        }

        var accessToken = _jwtTokenGenerator.Generate(
            user.ExternalId,
            user.Login,
            user.Email,
            user.Role,
            sessionId);

        return new LoginResponse(
            BearerTokenType,
            accessToken.Value,
            accessToken.ExpiresAt,
            rawRefreshToken,
            refreshToken.ExpiresAt);
    }

    // Eventos de falha em autocommit, fora de qualquer transacao. O login digitado numa conta inexistente nunca entra:
    // usuarios digitam a senha no campo de login por engano.
    private async Task RejectAsync(
        User? user,
        bool canVerifyRealHash,
        DateTimeOffset now)
    {
        if (canVerifyRealHash)
        {
            var lockedOut = await _userRepository.RecordFailedAccessAsync(user!.ExternalId, now);

            await RecordFailureAsync(user.ExternalId, AuditReason.InvalidPassword);

            if (lockedOut)
            {
                await _auditLog.RecordAsync(new AuditEvent(
                    AuditEventType.AccountLockedOut,
                    AuditOutcome.Failure,
                    user.ExternalId,
                    Reason: AuditReason.TooManyFailedAttempts));
            }
        }
        else
        {
            await RecordFailureAsync(user?.ExternalId, user is null ? AuditReason.UnknownLogin : AuditReason.LockedOut);
        }

        throw new InvalidCredentialsException();
    }

    private Task RecordFailureAsync(
        Guid? userExternalId,
        string reason)
    {
        return _auditLog.RecordAsync(new AuditEvent(AuditEventType.LoginFailed, AuditOutcome.Failure, userExternalId, Reason: reason));
    }

    private async Task IssueSessionAsync(
        User user,
        RefreshToken refreshToken,
        string? rehashedPassword,
        DateTimeOffset now)
    {
        // Zera o contador de falhas e grava last_login_at. Se a conta foi bloqueada entre a leitura e aqui, recusa
        // com o mesmo erro generico.
        if (!await _userRepository.TryRegisterLoginAsync(user.ExternalId, now))
        {
            throw new InvalidCredentialsException();
        }

        if (rehashedPassword is not null)
        {
            await _userRepository.TryRehashPasswordAsync(user.ExternalId, user.PasswordHash, rehashedPassword);
        }

        await _refreshTokenRepository.AddAsync(refreshToken);

        await _auditLog.RecordAsync(new AuditEvent(
            AuditEventType.LoginSucceeded,
            AuditOutcome.Success,
            user.ExternalId,
            SessionId: refreshToken.SessionId));
    }
}
