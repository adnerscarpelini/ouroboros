namespace Ouroboros.Services.Auth.Application.Tests;

public sealed class FakeEmailTemplateRenderer : IEmailTemplateRenderer
{
	public string? LastTemplateName { get; private set; }

	// Devolve os valores substituídos no corpo, para que o teste consiga afirmar o que o caso de uso
	// colocou no e-mail (o link com o token, por exemplo) sem depender do HTML real do template.
	public Task<string> RenderAsync(
		string templateName,
		IReadOnlyDictionary<string, string> placeholders,
		CancellationToken cancellationToken
	)
	{
		LastTemplateName = templateName;

		var renderedPlaceholders = string.Join("|", placeholders.Select(p => $"{p.Key}={p.Value}"));

		return Task.FromResult($"[{templateName}] {renderedPlaceholders}");
	}
}
