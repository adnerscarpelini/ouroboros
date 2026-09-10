using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Infrastructure.Tests;

public sealed class FakeMessagePublisher : IMessagePublisher
{
	private readonly List<MessageEnvelope> _publishedEnvelopes = [];

	// Quando definido, publicar mensagem desse tipo falha — usado para exercitar o caminho de erro.
	public string? FailingMessageType { get; set; }

	public IReadOnlyList<MessageEnvelope> PublishedEnvelopes => _publishedEnvelopes;

	public Task PublishAsync(
		MessageEnvelope envelope,
		CancellationToken cancellationToken
	)
	{
		if (envelope.MessageType == FailingMessageType)
		{
			throw new InvalidOperationException("transporte indisponivel");
		}

		_publishedEnvelopes.Add(envelope);

		return Task.CompletedTask;
	}
}
