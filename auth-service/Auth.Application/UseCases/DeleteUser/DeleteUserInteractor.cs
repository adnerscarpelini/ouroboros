namespace Ouroboros.Auth.Application.UseCases.DeleteUser;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;

public sealed class DeleteUserInteractor : IDeleteUserUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUserInteractor(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenRepository tokenRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenRepository = tokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeleteUserResponse> ExecuteAsync(DeleteUserRequest request)
    {
        // Comparacao exata com o nome: qualquer outro valor (inclusive numerico) cai no caminho sem privilegio.
        var isAdmin = request.RequesterRole == nameof(UserRole.Admin);
        var isSelf = request.ExternalId == request.RequesterId;

        // Autorizacao antes de consultar o banco: negar sem buscar nao revela se a conta pedida existe.
        if (!isAdmin && !isSelf)
        {
            throw new AccessDeniedException();
        }

        var requester = await _userRepository.GetByExternalIdAsync(request.RequesterId);

        // Reautenticacao: uma sessao aberta (ou um access token roubado) sozinha nao basta pra excluir uma conta.
        // A falha de senha e gravada em autocommit, fora da transacao da exclusao, pra contar pro bloqueio de conta.
        var now = DateTimeOffset.UtcNow;
        var canVerifyRealHash = requester is not null && !requester.IsLockedOut(now);
        var hash = canVerifyRealHash ? requester!.PasswordHash : _passwordHasher.DummyHash;
        var passwordMatches = _passwordHasher.Verify(request.RequesterPassword ?? string.Empty, hash);

        if (!canVerifyRealHash || !passwordMatches)
        {
            if (canVerifyRealHash)
            {
                await _userRepository.RecordFailedAccessAsync(requester!.ExternalId, now);
            }

            throw new InvalidCredentialsException();
        }

        if (!await _userRepository.TryResetFailedAccessAsync(requester!.ExternalId, now))
        {
            throw new InvalidCredentialsException();
        }

        var target = isSelf ? requester : await _userRepository.GetByExternalIdAsync(request.ExternalId);

        if (target is null)
        {
            throw new UserNotFoundException();
        }

        // Exclusao logica, fim das sessoes e invalidacao dos tokens pendentes valem juntos ou nao valem.
        return await _unitOfWork.ExecuteAsync(() => DeleteAsync(target.ExternalId, target.Role == UserRole.Admin && target.Active, now));
    }

    private async Task<DeleteUserResponse> DeleteAsync(
        Guid targetExternalId,
        bool targetIsActiveAdmin,
        DateTimeOffset now)
    {
        // Sem ao menos um Admin ativo ninguem mais consegue administrar o sistema. A contagem trava as linhas dos
        // Admins ate o fim da transacao: duas exclusoes simultaneas de Admins ficam em fila, e a segunda ja ve a
        // contagem reduzida. O lock vem antes da releitura do alvo, que precisa enxergar o que a primeira confirmou.
        var activeAdmins = targetIsActiveAdmin
            ? await _userRepository.CountActiveAdminsForUpdateAsync()
            : 0;

        // O alvo e lido de novo dentro da transacao: o UpdateAsync regrava a linha toda com o que foi lido, e uma
        // exclusao ou troca de senha confirmada nesse meio tempo nao pode ser desfeita.
        var user = await _userRepository.GetByExternalIdAsync(targetExternalId);

        if (user is null)
        {
            throw new UserNotFoundException();
        }

        if (targetIsActiveAdmin && user.Role == UserRole.Admin && user.Active && activeAdmins <= 1)
        {
            throw new DomainException("The last active admin cannot be deleted");
        }

        user.Delete();

        await _userRepository.UpdateAsync(user);

        await _refreshTokenRepository.RevokeAllActiveByUserAsync(user.ExternalId, now);
        await _tokenRepository.InvalidatePendingByUserAsync(
            user.ExternalId,
            TokenType.EmailConfirmation,
            now);
        await _tokenRepository.InvalidatePendingByUserAsync(
            user.ExternalId,
            TokenType.PasswordReset,
            now);

        return new DeleteUserResponse(user.ExternalId);
    }
}
