namespace Ouroboros.Auth.Application.UseCases.ResetPassword;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Ouroboros.Auth.Domain.Policies;

public sealed class ResetPasswordInteractor : IResetPasswordUseCase
{
    // Mesma mensagem pra token vazio, inexistente, de outro tipo, expirado ou ja usado: nao revela qual caso ocorreu.
    private const string InvalidTokenMessage = "Invalid or expired password reset token";

    private readonly ITokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBreachedPasswordChecker _breachedPasswordChecker;
    private readonly IAuditLog _auditLog;

    public ResetPasswordInteractor(
        ITokenRepository tokenRepository,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator,
        IUnitOfWork unitOfWork,
        IBreachedPasswordChecker breachedPasswordChecker,
        IAuditLog auditLog)
    {
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _unitOfWork = unitOfWork;
        _breachedPasswordChecker = breachedPasswordChecker;
        _auditLog = auditLog;
    }

    public async Task<ResetPasswordResponse> ExecuteAsync(ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new DomainException(InvalidTokenMessage);
        }

        var now = DateTimeOffset.UtcNow;
        var token = await _tokenRepository.GetByHashAsync(_tokenGenerator.Hash(request.Token), TokenType.PasswordReset);

        if (token is null || !token.IsPending(now))
        {
            throw new DomainException(InvalidTokenMessage);
        }

        var user = await _userRepository.GetByExternalIdAsync(token.UserExternalId);

        if (user is null)
        {
            throw new DomainException(InvalidTokenMessage);
        }

        if (!user.Active)
        {
            throw new DomainException("User is not active");
        }

        // Senha rejeitada nao consome o token: o usuario pode tentar de novo com o mesmo link.
        PasswordPolicy.Validate(request.NewPassword, user.Login, user.Email);

        if (await _breachedPasswordChecker.IsBreachedAsync(request.NewPassword))
        {
            throw new DomainException(PasswordPolicy.CommonOrBreachedMessage);
        }

        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            throw new DomainException("New password must be different from the current password");
        }

        // O hash e lento de proposito: calculado antes pra a transacao ficar aberta so pelo tempo das escritas.
        var newPasswordHash = _passwordHasher.Hash(request.NewPassword);

        // Consumo do token, troca da senha, desbloqueio e fim das sessoes valem juntos ou nao valem.
        return await _unitOfWork.ExecuteAsync(() => ResetAsync(token, newPasswordHash, now));
    }

    private async Task<ResetPasswordResponse> ResetAsync(
        Token token,
        string newPasswordHash,
        DateTimeOffset now)
    {
        token.MarkAsUsed(now);

        // Duas redefinicoes simultaneas chegam aqui juntas: so uma afeta a linha.
        if (!await _tokenRepository.TryMarkAsUsedAsync(token))
        {
            throw new DomainException(InvalidTokenMessage);
        }

        // O usuario e lido de novo depois de consumir o token, ja dentro da transacao: o UpdateAsync regrava a linha
        // toda com o que foi lido, e uma exclusao confirmada nesse meio tempo nao pode ser desfeita.
        var user = await _userRepository.GetByExternalIdAsync(token.UserExternalId);

        if (user is null || !user.Active)
        {
            throw new DomainException(InvalidTokenMessage);
        }

        user.ChangePassword(newPasswordHash);

        await _userRepository.UpdateAsync(user);

        // Quem conclui a redefinicao provou que controla o e-mail: o dono legitimo nao fica preso num bloqueio
        // causado por tentativas de forca bruta.
        await _userRepository.ClearLockoutAsync(user.ExternalId, now);

        // Encerra todas as sessoes, inclusive as de quem eventualmente tinha a senha antiga.
        await _refreshTokenRepository.RevokeAllActiveByUserAsync(user.ExternalId, now);

        await _auditLog.RecordAsync(new AuditEvent(AuditEventType.PasswordResetCompleted, AuditOutcome.Success, user.ExternalId));

        return new ResetPasswordResponse(user.ExternalId);
    }
}
