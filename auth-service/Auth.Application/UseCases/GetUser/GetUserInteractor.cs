namespace Ouroboros.Auth.Application.UseCases.GetUser;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Ouroboros.Auth.Domain.Policies;

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

        // O solicitante e lido do banco: o perfil e a identidade valem os de agora, nao os do token (que vale ate 15 min).
        // Conta inexistente ou excluida perde o acesso na hora, mesmo com o token ainda dentro do prazo.
        var requester = await _userRepository.GetByExternalIdAsync(request.RequesterId)
            ?? throw new InvalidAccessTokenException();

        var isAdmin = requester.Role == UserRole.Admin;
        var isSelf = IsRequestingSelf(requester, request.ExternalId, login, email);

        // Autorizacao antes de consultar o alvo: le-se o solicitante, nunca o alvo, pra negar sem revelar se a conta existe.
        if (!isAdmin && !isSelf)
        {
            throw new AccessDeniedException();
        }

        // Quem consulta a si mesmo ja esta carregado.
        var user = isSelf ? requester : await FindUserAsync(request.ExternalId, login, email);

        if (user is null)
        {
            throw new UserNotFoundException();
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

        return IdentityPolicy.Normalize(value);
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

    // Compara com os dados atuais do solicitante no banco, ja normalizados (spec 2026092508).
    private static bool IsRequestingSelf(
        User requester,
        Guid? externalId,
        string? login,
        string? email)
    {
        if (externalId is not null)
        {
            return externalId == requester.ExternalId;
        }

        if (login is not null)
        {
            return login == requester.NormalizedLogin;
        }

        return email == requester.NormalizedEmail;
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
