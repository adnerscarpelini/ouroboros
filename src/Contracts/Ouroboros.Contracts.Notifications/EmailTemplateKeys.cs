namespace Ouroboros.Contracts.Notifications;

// Catálogo fechado de templates que um produtor pode pedir. O consumidor rejeita qualquer chave fora
// desta lista: template não é nome de arquivo vindo pela mensagem.
public static class EmailTemplateKeys
{
	public const string AuthEmailConfirmation = "auth.email-confirmation";
	public const string AuthPasswordReset = "auth.password-reset";
}
