namespace Ouroboros.Auth.Application.UseCases.RegisterUser;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;

public sealed class RegisterUserInteractor : IRegisterUserUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterUserInteractor(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<RegisterUserResponse> ExecuteAsync(RegisterUserRequest request)
    {
        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(request.Login, request.FullName, request.Email, passwordHash);

        var alreadyExists = await _userRepository.ExistsByLoginOrEmailAsync(user.Login, user.Email);

        if (alreadyExists)
        {
            throw new DomainException("Login or email already in use");
        }

        await _userRepository.AddAsync(user);

        return new RegisterUserResponse(user.ExternalId, user.Login, user.FullName, user.Email);
    }
}
