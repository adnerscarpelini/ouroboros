namespace Ouroboros.Services.Auth.Application;

public interface IJwtKeyProvider
{
	// Só o Auth assina token; qualquer outro serviço apenas valida, e para isso precisa da chave
	// pública. Publicá-la num JWKS evita copiar o PEM para dentro da configuração de cada serviço.
	JwtPublicKey GetPublicKey();
}
