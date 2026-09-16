using Ouroboros.BuildingBlocks.Application;
using Ouroboros.Contracts.Notifications;

namespace Ouroboros.AuthService.Application.Tests;

public sealed class FakeOutboxMessageQueue : IOutboxMessageQueue
{
	private readonly List<EmailNotificationRequestedV1> _requests = [];

	public string? LastMessageType { get; private set; }
	public int? LastSchemaVersion { get; private set; }

	public IReadOnlyList<EmailNotificationRequestedV1> Requests => _requests;

	public EmailNotificationRequestedV1? LastRequest => _requests.LastOrDefault();

	public Guid Add<TPayload>(
		string messageType,
		int schemaVersion,
		TPayload payload
	)
	{
		LastMessageType = messageType;
		LastSchemaVersion = schemaVersion;

		if (payload is EmailNotificationRequestedV1 emailRequest)
		{
			_requests.Add(emailRequest);
		}

		return Guid.NewGuid();
	}
}
