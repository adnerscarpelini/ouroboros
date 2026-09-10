using Ouroboros.Contracts.Notifications;

namespace Ouroboros.Services.Notifications.Application;

public sealed record EmailTemplate(
	string Key,
	int Version,
	string Locale,
	// Quem tem permissão de pedir este template. Um produtor não dispara o e-mail de outro.
	string Producer,
	// O assunto faz parte do template versionado: o produtor manda dados, não texto pronto.
	string Subject,
	string FileName,
	IReadOnlyCollection<string> RequiredPlaceholders
);

// Catálogo fechado: template é escolhido por chave conhecida, nunca por caminho de arquivo vindo na
// mensagem. Uma chave desconhecida é rejeitada antes de qualquer acesso a disco.
public static class EmailTemplateCatalog
{
	private static readonly IReadOnlyList<EmailTemplate> Templates =
	[
		new EmailTemplate(
			Key: EmailTemplateKeys.AuthEmailConfirmation,
			Version: 1,
			Locale: "pt-BR",
			Producer: NotificationProducers.Auth,
			Subject: "Confirme seu cadastro",
			FileName: "auth.email-confirmation.v1.pt-BR.html",
			RequiredPlaceholders: ["FullName", "ConfirmationUrl"]
		),
		new EmailTemplate(
			Key: EmailTemplateKeys.AuthPasswordReset,
			Version: 1,
			Locale: "pt-BR",
			Producer: NotificationProducers.Auth,
			Subject: "Redefinição de senha",
			FileName: "auth.password-reset.v1.pt-BR.html",
			RequiredPlaceholders: ["FullName", "ResetUrl"]
		)
	];

	public static EmailTemplate? Find(
		string key,
		int version,
		string locale
	)
	{
		return Templates.FirstOrDefault(template =>
			template.Key == key
			&& template.Version == version
			&& template.Locale == locale);
	}
}
