namespace Ouroboros.Services.Auth.Application;

public sealed record AuthApplicationOptions(
	// URL pública do projeto (a do Api Gateway, não a porta interna do serviço). É ela que vai nos
	// links enviados por e-mail, então precisa ser um endereço que o usuário final consiga abrir.
	string PublicBaseUrl
);
