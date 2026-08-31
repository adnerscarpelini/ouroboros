namespace Ouroboros.Api.Contracts.Auth;

public sealed record LoginResponse(
	string AccessToken,
	DateTime ExpiresAt
);
