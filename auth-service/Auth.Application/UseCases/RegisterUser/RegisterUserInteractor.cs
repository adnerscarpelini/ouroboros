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

        var alreadyExists = await _userRepository.ExistsByLoginOrEmailAsync(user.Login, user.Email);

        if (alreadyExists)
        {
            throw new DomainException("Login or email already in use");
        }

        await _userRepository.AddAsync(user);

        var confirmationToken = _tokenGenerator.Generate();
        var token = Token.Create(
            user.ExternalId,
            TokenType.EmailConfirmation,
            _tokenGenerator.Hash(confirmationToken),
            DateTimeOffset.UtcNow.Add(EmailConfirmationTokenLifetime));

        await _tokenRepository.AddAsync(token);

        return new RegisterUserResponse(
            user.ExternalId,
            user.Login,
            user.FullName,
            user.Email,
            confirmationToken);
    }
}
