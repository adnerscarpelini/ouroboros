namespace Ouroboros.Modules.Auth.Application;

public sealed record AuthenticationResult(
	string AccessToken,
	DateTime ExpiresAt
);
