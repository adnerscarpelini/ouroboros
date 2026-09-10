using Microsoft.EntityFrameworkCore;
using Ouroboros.BuildingBlocks.Application;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure.Tests;

public class OutboxMessageQueueTests
{
	private static TestDbContext CreateDbContext()
	{
		var options = new DbContextOptionsBuilder<TestDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		return new TestDbContext(options);
	}

	private static OutboxMessageQueue CreateQueue(
		TestDbContext dbContext,
		string? correlationId = "correlation-1"
	)
	{
		return new OutboxMessageQueue(
			dbContext: dbContext,
			correlationIdAccessor: new FakeCorrelationIdAccessor { CorrelationId = correlationId },
			producer: new OutboxProducer("auth")
		);
	}

	[Fact]
	public async Task Add_DoesNotPersistUntilTheCallerSavesChanges()
	{
		await using var dbContext = CreateDbContext();
		var queue = CreateQueue(dbContext);

		queue.Add(
			messageType: "notifications.email.requested",
			schemaVersion: 1,
			payload: new { Recipient = "user@example.com" }
		);

		// O ponto do padrão: quem confirma é a transação do caso de uso, não a fila. Antes do
		// SaveChanges nada existe no banco — é isso que mantém dado de negócio e mensagem juntos.
		Assert.Empty(await dbContext.OutboxMessages.ToListAsync());

		await dbContext.SaveChangesAsync();

		Assert.Single(await dbContext.OutboxMessages.ToListAsync());
	}

	[Fact]
	public async Task Add_ReturnsMessageIdThatMatchesThePersistedRow()
	{
		await using var dbContext = CreateDbContext();
		var queue = CreateQueue(dbContext);

		var messageId = queue.Add(
			messageType: "notifications.email.requested",
			schemaVersion: 1,
			payload: new { Recipient = "user@example.com" }
		);

		await dbContext.SaveChangesAsync();

		var outboxMessage = await dbContext.OutboxMessages.SingleAsync();
		Assert.Equal(messageId, outboxMessage.ExternalId);
		Assert.NotEqual(Guid.Empty, messageId);
	}

	[Fact]
	public async Task Add_RecordsProducerCorrelationAndSerializedPayload()
	{
		await using var dbContext = CreateDbContext();
		var queue = CreateQueue(dbContext);

		queue.Add(
			messageType: "notifications.email.requested",
			schemaVersion: 1,
			payload: new { Recipient = "user@example.com" }
		);

		await dbContext.SaveChangesAsync();

		var outboxMessage = await dbContext.OutboxMessages.SingleAsync();
		Assert.Equal("auth", outboxMessage.Producer);
		Assert.Equal("notifications.email.requested", outboxMessage.MessageType);
		Assert.Equal(1, outboxMessage.SchemaVersion);
		Assert.Equal("correlation-1", outboxMessage.CorrelationId);
		Assert.Contains("user@example.com", outboxMessage.Payload);
		Assert.Equal(OutboxMessageStatus.Pending, outboxMessage.Status);
	}

	[Fact]
	public async Task Add_AcceptsNoCorrelationId()
	{
		await using var dbContext = CreateDbContext();
		var queue = CreateQueue(dbContext, correlationId: null);

		queue.Add(
			messageType: "notifications.email.requested",
			schemaVersion: 1,
			payload: new { Recipient = "user@example.com" }
		);

		await dbContext.SaveChangesAsync();

		var outboxMessage = await dbContext.OutboxMessages.SingleAsync();
		Assert.Null(outboxMessage.CorrelationId);
	}
}
