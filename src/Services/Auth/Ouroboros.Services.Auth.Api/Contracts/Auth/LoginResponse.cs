namespace Ouroboros.Services.Auth.Api.Contracts.Auth;

public sealed record LoginResponse(
	string AccessToken,
	DateTime ExpiresAt,
	string RefreshToken,
	DateTime RefreshTokenExpiresAt
);
