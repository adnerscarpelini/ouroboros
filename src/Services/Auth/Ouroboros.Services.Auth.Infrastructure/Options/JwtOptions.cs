namespace Ouroboros.Services.Auth.Infrastructure;

public sealed record JwtOptions(
	// Chave PRIVADA RSA (PEM) — só o Auth tem. Assina o JWT; nunca sai daqui.
	string SigningKeyPem,
	// Chave PÚBLICA RSA (PEM) — não é segredo. Valida o token e é o que o JWKS publica
	// para que outros serviços validem sem receber nenhum PEM por configuração.
	string PublicKeyPem,
	string Issuer,
	string Audience
);
