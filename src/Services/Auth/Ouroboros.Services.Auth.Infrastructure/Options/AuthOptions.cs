namespace Ouroboros.Services.Auth.Infrastructure;

public sealed record AuthOptions(
	string ApiBaseUrl,
	// Chave PRIVADA RSA (PEM) — só o Auth tem. Assina o JWT; nunca sai daqui.
	// Quem só precisa validar o token (qualquer outro serviço) usa a chave pública correspondente.
	string JwtSigningKeyPem,
	string JwtIssuer,
	string JwtAudience
);
