namespace Ouroboros.Auth.Application.UseCases.GetUser;

/// <remarks>
/// Allowlist de campos: nunca incluir id interno, hash de senha, datas de senha/login ou tokens.
/// </remarks>
public record GetUserResponse(
    Guid ExternalId,
    string Login,
    string FullName,
    string Email,
    string Role,
    bool Active,
    DateTimeOffset CreatedAt);
