namespace Ouroboros.Auth.Application.UseCases.GetUser;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;

public sealed class GetUserInteractor : IGetUserUseCase
{
    private readonly IUserRepository _userRepository;

    public GetUserInteractor(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<GetUserResponse> ExecuteAsync(GetUserRequest request)
    {
        var login = Normalize(request.Login);
        var email = Normalize(request.Email);

        ValidateSingleCriterion(request.ExternalId, login, email);

        // Comparacao exata com o nome: qualquer outro valor (inclusive numerico) cai no caminho sem privilegio.
        var isAdmin = request.RequesterRole == nameof(UserRole.Admin);

        // Autorizacao antes de consultar o banco: negar sem buscar nao revela se a conta pedida existe.
        if (!isAdmin && !IsRequestingSelf(request, login, email))
        {
            throw new AccessDeniedException();
        }

        var user = await FindUserAsync(request.ExternalId, login, email);

        if (user is null)
        {
            throw new UserNotFoundException();
        }

        if (!isAdmin && user.ExternalId != request.RequesterId)
        {
            throw new AccessDeniedException();
        }

        return new GetUserResponse(
            user.ExternalId,
            user.Login,
            user.FullName,
            user.Email,
            user.Role.ToString(),
            user.Active,
            user.CreatedAt);
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static void ValidateSingleCriterion(
        Guid? externalId,
        string? login,
        string? email)
    {
        var criteriaCount = 0;

        if (externalId is not null)
        {
            criteriaCount++;
        }

        if (login is not null)
        {
            criteriaCount++;
        }

        if (email is not null)
        {
            criteriaCount++;
        }

        if (criteriaCount != 1)
        {
            throw new DomainException("Exactly one search criterion (externalId, login or email) is required");
        }
    }

    private static bool IsRequestingSelf(
        GetUserRequest request,
        string? login,
        string? email)
    {
        if (request.ExternalId is not null)
        {
            return request.ExternalId == request.RequesterId;
        }

        if (login is not null)
        {
            return login == request.RequesterLogin;
        }

        return email == request.RequesterEmail;
    }

    private Task<User?> FindUserAsync(
        Guid? externalId,
        string? login,
        string? email)
    {
        if (externalId is not null)
        {
            return _userRepository.GetByExternalIdAsync(externalId.Value);
        }

        if (login is not null)
        {
            return _userRepository.GetByLoginAsync(login);
        }

        return _userRepository.GetByEmailAsync(email!);
    }
}
