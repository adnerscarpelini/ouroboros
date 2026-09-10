namespace Ouroboros.Services.Notifications.Application;

public interface IEmailTemplateRenderer
{
	// Mantém o acesso ao arquivo do template fora do caso de uso: a Application decide qual template
	// e quais valores usar, a Infrastructure decide de onde o HTML vem e como escapá-lo.
	Task<RenderedEmail> RenderAsync(
		EmailTemplate template,
		IReadOnlyDictionary<string, string> data,
		// Cancela a operação em andamento se a aplicação estiver sendo encerrada.
		CancellationToken cancellationToken
	);
}
