namespace Ouroboros.Modules.Auth.Infrastructure;

public sealed record AuthOptions(
	string ApiBaseUrl,
	string JwtSigningKey,
	string JwtIssuer,
	string JwtAudience
);
