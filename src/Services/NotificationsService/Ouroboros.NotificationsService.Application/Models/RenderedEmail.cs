namespace Ouroboros.NotificationsService.Application;

public sealed record RenderedEmail(
	string Subject,
	string BodyHtml
);
