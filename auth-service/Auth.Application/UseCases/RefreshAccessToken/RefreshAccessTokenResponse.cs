namespace Ouroboros.Auth.Application.UseCases.RefreshAccessToken;

public record RefreshAccessTokenResponse(
    string TokenType,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
