using Ouroboros.Contracts.Notifications;
using Ouroboros.NotificationsService.Application;
using Ouroboros.NotificationsService.Infrastructure;

namespace Ouroboros.NotificationsService.Infrastructure.Tests;

public class EmailTemplateRendererTests
{
	private static EmailTemplate ConfirmationTemplate()
	{
		return EmailTemplateCatalog.Find(
			key: EmailTemplateKeys.AuthEmailConfirmation,
			version: 1,
			locale: "pt-BR"
		)!;
	}

	[Fact]
	public async Task RenderAsync_ReplacesPlaceholdersAndUsesTheVersionedSubject()
	{
		var template = ConfirmationTemplate();

		var rendered = await new EmailTemplateRendererService().RenderAsync(
			template: template,
			data: new Dictionary<string, string>
			{
				["FullName"] = "João Silva",
				["ConfirmationUrl"] = "http://localhost:5082/api/auth/confirm-email?token=abc"
			},
			cancellationToken: CancellationToken.None
		);

		// O assunto vem do template versionado, não da mensagem do produtor.
		Assert.Equal(template.Subject, rendered.Subject);
		// O escape de HTML converte acentos em entidades numéricas ("Jo&#227;o"). O cliente de e-mail
		// renderiza o mesmo texto, e a alternativa seria escrever um escapador próprio só para
		// preservar bytes que o leitor nunca vê.
		Assert.Contains("Jo&#227;o Silva", rendered.BodyHtml);
		Assert.Contains("token=abc", rendered.BodyHtml);
		Assert.DoesNotContain("{{FullName}}", rendered.BodyHtml);
		Assert.DoesNotContain("{{ConfirmationUrl}}", rendered.BodyHtml);
	}

	[Fact]
	public async Task RenderAsync_EscapesValuesThatCameFromUserInput()
	{
		var rendered = await new EmailTemplateRendererService().RenderAsync(
			template: ConfirmationTemplate(),
			data: new Dictionary<string, string>
			{
				["FullName"] = "<script>alert(1)</script>",
				["ConfirmationUrl"] = "http://localhost:5082/api/auth/confirm-email?token=abc"
			},
			cancellationToken: CancellationToken.None
		);

		// O nome vem de um cadastro público: sem escape, ele entraria intacto no corpo do e-mail.
		Assert.DoesNotContain("<script>", rendered.BodyHtml);
		Assert.Contains("&lt;script&gt;", rendered.BodyHtml);
	}

	[Fact]
	public async Task RenderAsync_WithMissingPlaceholder_Fails()
	{
		var renderer = new EmailTemplateRendererService();

		await Assert.ThrowsAsync<InvalidOperationException>(() => renderer.RenderAsync(
			template: ConfirmationTemplate(),
			data: new Dictionary<string, string> { ["FullName"] = "João Silva" },
			cancellationToken: CancellationToken.None
		));
	}
}
