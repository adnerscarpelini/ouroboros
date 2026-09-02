namespace Ouroboros.Modules.Auth.Application;

public sealed record AccessTokenResult(
	string AccessToken,
	DateTime ExpiresAt
);
