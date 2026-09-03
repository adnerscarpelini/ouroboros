using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.Services.Auth.Infrastructure.Tests;

public sealed class FakeEmailQueueService : IEmailQueueService
{
	public string? LastSubject { get; private set; }
	public string? LastBodyHtml { get; private set; }
	public string? LastRecipient { get; private set; }

	public Task<long> EnqueueAsync(
		string subject,
		string bodyHtml,
		string recipient,
		CancellationToken cancellationToken
	)
	{
		LastSubject = subject;
		LastBodyHtml = bodyHtml;
		LastRecipient = recipient;

		return Task.FromResult(1L);
	}
}
