namespace Ouroboros.Services.Auth.Application;

public sealed record AccessTokenResult(
	string AccessToken,
	DateTime ExpiresAt
);
