namespace Ouroboros.Auth.Application.Gateways;

public record AccessToken(
    string Value,
    DateTimeOffset ExpiresAt);
