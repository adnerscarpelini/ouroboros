namespace Ouroboros.Auth.Application.UseCases.CleanupExpiredTokens;

public record CleanupExpiredTokensResponse(
    int Tokens,
    int RefreshTokens);
