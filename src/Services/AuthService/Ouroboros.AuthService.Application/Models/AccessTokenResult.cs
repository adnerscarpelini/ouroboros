namespace Ouroboros.AuthService.Application;

public sealed record AccessTokenResult(
	string AccessToken,
	DateTime ExpiresAt
);
