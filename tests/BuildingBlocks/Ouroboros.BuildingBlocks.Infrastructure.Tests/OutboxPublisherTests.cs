using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.BuildingBlocks.Application;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure.Tests;

public class OutboxPublisherTests
{
	private static readonly OutboxOptions Options = new(
		PollingInterval: TimeSpan.FromSeconds(15),
		BatchSize: 20,
		InitialRetryDelay: TimeSpan.FromSeconds(1),
		MaxRetryDelay: TimeSpan.FromMinutes(5)
	);

	private static TestDbContext CreateDbContext()
	{
		var options = new DbContextOptionsBuilder<TestDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		return new TestDbContext(options);
	}

	private static OutboxPublisher<TestDbContext> CreatePublisher(
		TestDbContext dbContext,
		FakeMessagePublisher messagePublisher
	)
	{
		return new OutboxPublisher<TestDbContext>(
			dbContext: dbContext,
			messagePublisher: messagePublisher,
			options: Options,
			logger: NullLogger<OutboxPublisher<TestDbContext>>.Instance
		);
	}

	private static OutboxMessage AddPendingMessage(
		TestDbContext dbContext,
		string messageType = "notifications.email.requested"
	)
	{
		var outboxMessage = new OutboxMessage(
			producer: "auth",
			messageType: messageType,
			schemaVersion: 1,
			payload: "{}",
			correlationId: "correlation-1"
		);

		dbContext.OutboxMessages.Add(outboxMessage);

		return outboxMessage;
	}

	[Fact]
	public async Task PublishPendingAsync_PublishesPendingMessageAndMarksItAsPublished()
	{
		await using var dbContext = CreateDbContext();
		var outboxMessage = AddPendingMessage(dbContext);
		await dbContext.SaveChangesAsync();

		var messagePublisher = new FakeMessagePublisher();

		var published = await CreatePublisher(dbContext, messagePublisher)
			.PublishPendingAsync(CancellationToken.None);

		Assert.Equal(1, published);
		Assert.Equal(OutboxMessageStatus.Published, outboxMessage.Status);

		var envelope = Assert.Single(messagePublisher.PublishedEnvelopes);
		Assert.Equal(outboxMessage.ExternalId, envelope.MessageId);
		Assert.Equal("auth", envelope.Producer);
		Assert.Equal("correlation-1", envelope.CorrelationId);
	}

	[Fact]
	public async Task PublishPendingAsync_IgnoresMessagesAlreadyPublished()
	{
		await using var dbContext = CreateDbContext();
		var outboxMessage = AddPendingMessage(dbContext);
		outboxMessage.MarkAsPublished();
		await dbContext.SaveChangesAsync();

		var messagePublisher = new FakeMessagePublisher();

		var published = await CreatePublisher(dbContext, messagePublisher)
			.PublishPendingAsync(CancellationToken.None);

		Assert.Equal(0, published);
		Assert.Empty(messagePublisher.PublishedEnvelopes);
	}

	[Fact]
	public async Task PublishPendingAsync_KeepsMessagePendingWhenTransportFails()
	{
		await using var dbContext = CreateDbContext();
		var outboxMessage = AddPendingMessage(dbContext);
		await dbContext.SaveChangesAsync();

		var messagePublisher = new FakeMessagePublisher
		{
			FailingMessageType = "notifications.email.requested"
		};

		await CreatePublisher(dbContext, messagePublisher).PublishPendingAsync(CancellationToken.None);

		// Transporte fora do ar atrasa a notificação; não a perde.
		Assert.Equal(OutboxMessageStatus.Pending, outboxMessage.Status);
		Assert.Equal(1, outboxMessage.AttemptCount);
		Assert.NotNull(outboxMessage.NextAttemptAt);
		Assert.Contains("transporte indisponivel", outboxMessage.LastError);
	}

	[Fact]
	public async Task PublishPendingAsync_DoesNotRetryBeforeTheScheduledInstant()
	{
		await using var dbContext = CreateDbContext();
		var outboxMessage = AddPendingMessage(dbContext);
		outboxMessage.RegisterFailedAttempt(
			error: "transporte indisponivel",
			nextAttemptAt: DateTime.UtcNow.AddMinutes(5)
		);
		await dbContext.SaveChangesAsync();

		var messagePublisher = new FakeMessagePublisher();

		var published = await CreatePublisher(dbContext, messagePublisher)
			.PublishPendingAsync(CancellationToken.None);

		Assert.Equal(0, published);
		Assert.Empty(messagePublisher.PublishedEnvelopes);
	}

	[Fact]
	public async Task PublishPendingAsync_KeepsPublishingTheBatchAfterOneMessageFails()
	{
		await using var dbContext = CreateDbContext();
		var failingMessage = AddPendingMessage(dbContext, messageType: "notifications.email.failing");
		var healthyMessage = AddPendingMessage(dbContext);
		await dbContext.SaveChangesAsync();

		var messagePublisher = new FakeMessagePublisher
		{
			FailingMessageType = "notifications.email.failing"
		};

		var published = await CreatePublisher(dbContext, messagePublisher)
			.PublishPendingAsync(CancellationToken.None);

		Assert.Equal(2, published);
		Assert.Equal(OutboxMessageStatus.Pending, failingMessage.Status);
		Assert.Equal(OutboxMessageStatus.Published, healthyMessage.Status);
		Assert.Single(messagePublisher.PublishedEnvelopes);
	}
}
