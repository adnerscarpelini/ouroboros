using Ouroboros.NotificationsService.Application;

namespace Ouroboros.NotificationsService.Application.Tests;

public sealed class FakeEmailTemplateRenderer : IEmailTemplateRenderer
{
	public EmailTemplate? LastTemplate { get; private set; }
	public int RenderCount { get; private set; }

	public Task<RenderedEmail> RenderAsync(
		EmailTemplate template,
		IReadOnlyDictionary<string, string> data,
		CancellationToken cancellationToken
	)
	{
		LastTemplate = template;
		RenderCount++;

		return Task.FromResult(new RenderedEmail(
			Subject: template.Subject,
			BodyHtml: $"<p>{string.Join("|", data.Values)}</p>"
		));
	}
}
