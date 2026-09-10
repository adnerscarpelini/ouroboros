namespace Ouroboros.Services.Notifications.Application;

public sealed record RenderedEmail(
	string Subject,
	string BodyHtml
);
