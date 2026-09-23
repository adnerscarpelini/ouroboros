namespace Ouroboros.Auth.Application.UseCases.RegisterUser;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Ouroboros.Auth.Domain.Policies;

public sealed class RegisterUserInteractor : IRegisterUserUseCase
{
    private static readonly TimeSpan EmailConfirmationTokenLifetime = TimeSpan.FromHours(24);

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenRepository _tokenRepository;
    private readonly ITokenGenerator _tokenGenerator;

    public RegisterUserInteractor(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenRepository tokenRepository,
        ITokenGenerator tokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenRepository = tokenRepository;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<RegisterUserResponse> ExecuteAsync(RegisterUserRequest request)
    {
        PasswordPolicy.Validate(request.Password);

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(request.Login, request.FullName, request.Email, passwordHash);
        var now = DateTimeOffset.UtcNow;

        var loginOwner = await _userRepository.GetByLoginAsync(user.Login);
        var loginOwnerIsAbandoned = loginOwner is not null && await IsAbandonedAsync(loginOwner, now);

        // Login ocupado pode ser revelado: quem escolhe o login precisa saber que ele nao esta disponivel.
        if (loginOwner is not null && !loginOwnerIsAbandoned)
        {
            throw new DomainException("Login already in use");
        }

        var emailOwner = await _userRepository.GetByEmailAsync(user.Email);
        var emailOwnerIsAbandoned = emailOwner is not null && await IsAbandonedAsync(emailOwner, now);

        // E-mail ocupado termina sem erro e sem criar nada: o chamador nao pode descobrir se o e-mail tem conta.
        // TODO: quando existir envio de e-mail via mensageria, avisar o dono do e-mail sobre a tentativa de cadastro.
        if (emailOwner is not null && !emailOwnerIsAbandoned)
        {
            return new RegisterUserResponse(null, null);
        }

        if (loginOwner is not null)
        {
            await _userRepository.RemoveAsync(loginOwner.ExternalId);
        }

        if (emailOwner is not null && emailOwner.ExternalId != loginOwner?.ExternalId)
        {
            await _userRepository.RemoveAsync(emailOwner.ExternalId);
        }

        await _userRepository.AddAsync(user);

        var confirmationToken = _tokenGenerator.Generate();
        var token = Token.Create(
            user.ExternalId,
            TokenType.EmailConfirmation,
            _tokenGenerator.Hash(confirmationToken),
            now.Add(EmailConfirmationTokenLifetime));

        await _tokenRepository.AddAsync(token);

        return new RegisterUserResponse(user.ExternalId, confirmationToken);
    }

    // Cadastro abandonado: e-mail nunca confirmado e sem token de confirmacao pendente (passou das 24h).
    // Libera o login/e-mail pra um novo cadastro, pra que um cadastro de e-mail alheio nao bloqueie o dono pra sempre.
    private async Task<bool> IsAbandonedAsync(
        User user,
        DateTimeOffset now)
    {
        if (user.EmailConfirmed)
        {
            return false;
        }

        var hasPendingToken = await _tokenRepository.ExistsPendingByUserAsync(
            user.ExternalId,
            TokenType.EmailConfirmation,
            now);

        return !hasPendingToken;
    }
}
