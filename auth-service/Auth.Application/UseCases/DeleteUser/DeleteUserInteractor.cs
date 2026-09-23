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

    public DeleteUserInteractor(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenRepository tokenRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenRepository = tokenRepository;
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
        if (requester is null
            || string.IsNullOrEmpty(request.RequesterPassword)
            || !_passwordHasher.Verify(request.RequesterPassword, requester.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        var user = isSelf ? requester : await _userRepository.GetByExternalIdAsync(request.ExternalId);

        if (user is null)
        {
            throw new UserNotFoundException();
        }

        // Sem ao menos um Admin ativo ninguem mais consegue administrar o sistema.
        if (user.Role == UserRole.Admin && user.Active && await _userRepository.CountActiveAdminsAsync() <= 1)
        {
            throw new DomainException("The last active admin cannot be deleted");
        }

        user.Delete();

        await _userRepository.UpdateAsync(user);

        var now = DateTimeOffset.UtcNow;

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
