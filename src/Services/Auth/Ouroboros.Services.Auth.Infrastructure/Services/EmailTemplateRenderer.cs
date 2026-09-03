using Ouroboros.Services.Auth.Application;

namespace Ouroboros.Services.Auth.Infrastructure;

public sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
	public async Task<string> RenderAsync(
		string templateName,
		IReadOnlyDictionary<string, string> placeholders,
		CancellationToken cancellationToken
	)
	{
		var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", $"{templateName}.html");

		var renderedTemplate = await File.ReadAllTextAsync(templatePath, cancellationToken);

		foreach (var placeholder in placeholders)
		{
			renderedTemplate = renderedTemplate.Replace("{{" + placeholder.Key + "}}", placeholder.Value);
		}

		return renderedTemplate;
	}
}
