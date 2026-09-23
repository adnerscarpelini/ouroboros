namespace Ouroboros.Auth.Application.UseCases.RequestPasswordReset;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;

public sealed class RequestPasswordResetInteractor : IRequestPasswordResetUseCase
{
    private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    private readonly IUserRepository _userRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly ITokenGenerator _tokenGenerator;

    public RequestPasswordResetInteractor(
        IUserRepository userRepository,
        ITokenRepository tokenRepository,
        ITokenGenerator tokenGenerator)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<RequestPasswordResetResponse> ExecuteAsync(RequestPasswordResetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LoginOrEmail))
        {
            throw new DomainException("Login or email is required");
        }

        var user = await _userRepository.GetByLoginOrEmailAsync(request.LoginOrEmail.Trim());

        // Usuario inexistente, inativo ou sem e-mail confirmado termina sem erro: o chamador nao pode
        // distinguir esses casos (evita enumeracao) e o reset nao vira atalho pra ativar conta.
        if (user is null || !user.Active || !user.EmailConfirmed)
        {
            return new RequestPasswordResetResponse(null, null);
        }

        var now = DateTimeOffset.UtcNow;

        await _tokenRepository.InvalidatePendingByUserAsync(user.ExternalId, TokenType.PasswordReset, now);

        var passwordResetToken = _tokenGenerator.Generate();
        var token = Token.Create(
            user.ExternalId,
            TokenType.PasswordReset,
            _tokenGenerator.Hash(passwordResetToken),
            now.Add(PasswordResetTokenLifetime));

        await _tokenRepository.AddAsync(token);

        return new RequestPasswordResetResponse(user.ExternalId, passwordResetToken);
    }
}
