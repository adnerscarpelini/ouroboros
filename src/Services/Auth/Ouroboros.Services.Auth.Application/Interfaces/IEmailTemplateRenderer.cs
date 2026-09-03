namespace Ouroboros.Services.Auth.Application;

public interface IEmailTemplateRenderer
{
	// Mantém o acesso ao arquivo do template fora do caso de uso: a Application decide qual template
	// e quais valores usar, a Infrastructure decide de onde o HTML vem.
	Task<string> RenderAsync(
		string templateName,
		IReadOnlyDictionary<string, string> placeholders,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
