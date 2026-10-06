namespace Ouroboros.Auth.Application.UseCases.ChangePassword;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Exceptions;
using Ouroboros.Auth.Domain.Policies;

public sealed class ChangePasswordInteractor : IChangePasswordUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IBreachedPasswordChecker _breachedPasswordChecker;
    private readonly IUnitOfWork _unitOfWork;

    public ChangePasswordInteractor(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IBreachedPasswordChecker breachedPasswordChecker,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _breachedPasswordChecker = breachedPasswordChecker;
        _unitOfWork = unitOfWork;
    }

    public async Task<ChangePasswordResponse> ExecuteAsync(ChangePasswordRequest request)
    {
        // O usuario e lido do banco: conta inexistente, excluida ou inativa perde o acesso na hora, mesmo com o token
        // ainda dentro do prazo.
        var user = await _userRepository.GetByExternalIdAsync(request.UserId);

        if (user is null || !user.Active)
        {
            throw new InvalidAccessTokenException();
        }

        // Reautenticacao pela senha atual (OWASP): um access token roubado sozinho nao basta pra trocar a credencial.
        // A falha e gravada em autocommit, fora da transacao, pra contar pro bloqueio de conta. Conta bloqueada recebe a
        // mesma resposta de senha errada.
        var now = DateTimeOffset.UtcNow;
        var canVerifyRealHash = !user.IsLockedOut(now);
        var hash = canVerifyRealHash ? user.PasswordHash : _passwordHasher.DummyHash;
        var passwordMatches = _passwordHasher.Verify(request.CurrentPassword ?? string.Empty, hash);

        if (!canVerifyRealHash || !passwordMatches)
        {
            if (canVerifyRealHash)
            {
                await _userRepository.RecordFailedAccessAsync(user.ExternalId, now);
            }

            throw new InvalidCredentialsException();
        }

        // A nova senha segue a politica e nao pode ser a atual. Senha rejeitada nao conta como falha de bloqueio.
        PasswordPolicy.Validate(request.NewPassword, user.Login, user.Email);

        if (await _breachedPasswordChecker.IsBreachedAsync(request.NewPassword))
        {
            throw new DomainException(PasswordPolicy.CommonOrBreachedMessage);
        }

        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            throw new DomainException("New password must be different from the current password");
        }

        // O hash e lento de proposito: calculado fora da transacao, que fica aberta so pelo tempo das escritas.
        var newPasswordHash = _passwordHasher.Hash(request.NewPassword);

        // Novo hash, fim das outras sessoes e zeragem do contador valem juntos ou nao valem.
        // TODO: avisar o dono da conta por e-mail sobre a troca (padrao de mercado) quando existir envio de e-mail.
        return await _unitOfWork.ExecuteAsync(() => ChangeAsync(request, newPasswordHash, now));
    }

    private async Task<ChangePasswordResponse> ChangeAsync(
        ChangePasswordRequest request,
        string newPasswordHash,
        DateTimeOffset now)
    {
        // Relido dentro da transacao: o UpdateAsync regrava a linha toda com o que foi lido, e uma exclusao de conta
        // confirmada nesse meio tempo nao pode ser desfeita.
        var user = await _userRepository.GetByExternalIdAsync(request.UserId);

        if (user is null || !user.Active)
        {
            throw new InvalidAccessTokenException();
        }

        user.ChangePassword(newPasswordHash);

        await _userRepository.UpdateAsync(user);

        await _userRepository.ClearLockoutAsync(user.ExternalId, now);

        // Quem trocou a senha continua logado: so as outras sessoes (outros dispositivos) caem.
        await _refreshTokenRepository.RevokeAllActiveByUserExceptSessionAsync(
            user.ExternalId,
            request.SessionId ?? Guid.Empty,
            now);

        return new ChangePasswordResponse(user.ExternalId);
    }
}
