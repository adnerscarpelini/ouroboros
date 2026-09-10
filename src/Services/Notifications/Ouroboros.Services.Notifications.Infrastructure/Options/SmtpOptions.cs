namespace Ouroboros.Services.Notifications.Infrastructure;

public sealed record SmtpOptions(
	string Host,
	int Port,
	string FromAddress,
	string FromName,
	// "Auto" usa TLS se o servidor anunciar e texto puro se não — serve ao Mailpit do desenvolvimento.
	// Um provedor real exige política explícita ("StartTls" ou "SslOnConnect"): herdar o fallback
	// implícito em produção significaria aceitar entregar credencial em texto puro.
	string SecureSocketOptions,
	// Opcionais e com default explícito: o binder de configuração exige que todo parâmetro sem valor
	// padrão tenha entrada correspondente, e o Mailpit do desenvolvimento não pede autenticação.
	string? Username = null,
	string? Password = null
);
