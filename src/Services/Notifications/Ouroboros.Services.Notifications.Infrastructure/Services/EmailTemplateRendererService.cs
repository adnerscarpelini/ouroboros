using System.Net;
using Ouroboros.Services.Notifications.Application;

namespace Ouroboros.Services.Notifications.Infrastructure;

public sealed class EmailTemplateRendererService : IEmailTemplateRenderer
{
	public async Task<RenderedEmail> RenderAsync(
		EmailTemplate template,
		IReadOnlyDictionary<string, string> data,
		CancellationToken cancellationToken
	)
	{
		// O nome do arquivo vem do catálogo, nunca da mensagem: um TemplateKey desconhecido é
		// rejeitado na aceitação, antes de chegar aqui, e nenhum caminho vindo pela rede toca o disco.
		var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", template.FileName);

		if (!File.Exists(templatePath))
		{
			// Falha operacional tratável, não perda silenciosa: a entrega registra o erro e continua
			// visível na tabela, em vez de sumir.
			throw new InvalidOperationException($"Template '{template.FileName}' não encontrado em '{templatePath}'.");
		}

		var renderedTemplate = await File.ReadAllTextAsync(templatePath, cancellationToken);

		foreach (var placeholder in template.RequiredPlaceholders)
		{
			if (!data.TryGetValue(placeholder, out var value))
			{
				throw new InvalidOperationException($"Placeholder obrigatório '{placeholder}' ausente para o template '{template.Key}'.");
			}

			// Escape de HTML em todo valor: o nome do usuário chega de um cadastro público, e sem isso
			// um "<script>" digitado no cadastro entraria intacto no corpo do e-mail. As URLs são
			// montadas pelo produtor com o token já escapado por Uri.EscapeDataString, e o escape de
			// HTML aqui preserva esse conteúdo dentro do atributo href. Acentos viram entidades
			// numéricas ("Jo&#227;o"), o que o cliente de e-mail renderiza como o texto original.
			renderedTemplate = renderedTemplate.Replace(
				"{{" + placeholder + "}}",
				WebUtility.HtmlEncode(value)
			);
		}

		return new RenderedEmail(
			Subject: template.Subject,
			BodyHtml: renderedTemplate
		);
	}
}
