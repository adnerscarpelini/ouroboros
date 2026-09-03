using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.BuildingBlocks.Application;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure.Tests;

public class EmailOutboxDispatcherTests
{
	private static TestDbContext CreateDbContext()
	{
		var options = new DbContextOptionsBuilder<TestDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		var dbContext = new TestDbContext(options);
		dbContext.Database.EnsureCreated();

		return dbContext;
	}

	private static EmailOutboxOptions CreateOptions(
		int batchSize = 20,
		int maxAttempts = 3
	)
	{
		return new EmailOutboxOptions(
			SmtpHost: "localhost",
			SmtpPort: 1025,
			FromAddress: "nao-responda@ouroboros.local",
			FromName: "Ouroboros",
			PollingInterval: TimeSpan.FromSeconds(15),
			BatchSize: batchSize,
			MaxAttempts: maxAttempts
		);
	}

	private static EmailOutboxDispatcher<TestDbContext> CreateDispatcher(
		TestDbContext dbContext,
		FakeEmailSender emailSender,
		EmailOutboxOptions options
	)
	{
		return new EmailOutboxDispatcher<TestDbContext>(
			dbContext: dbContext,
			emailSender: emailSender,
			options: options,
			logger: NullLogger<EmailOutboxDispatcher<TestDbContext>>.Instance
		);
	}

	private static void AddPendingMessage(
		TestDbContext dbContext,
		string recipient
	)
	{
		dbContext.EmailMessages.Add(new EmailMessage(
			subject: "Confirme seu cadastro",
			bodyHtml: "<p>Ola!</p>",
			recipient: recipient
		));
	}

	[Fact]
	public async Task DispatchPendingAsync_WithPendingMessage_SendsAndMarksAsSent()
	{
		await using var dbContext = CreateDbContext();
		AddPendingMessage(dbContext, recipient: "user@example.com");
		await dbContext.SaveChangesAsync();

		var emailSender = new FakeEmailSender();
		var dispatcher = CreateDispatcher(dbContext, emailSender, CreateOptions());

		var processed = await dispatcher.DispatchPendingAsync(CancellationToken.None);

		Assert.Equal(1, processed);
		Assert.Equal("user@example.com", Assert.Single(emailSender.SentRecipients));

		var emailMessage = await dbContext.EmailMessages.SingleAsync();
		Assert.True(emailMessage.Sent);
		Assert.NotNull(emailMessage.SentAt);
	}

	[Fact]
	public async Task DispatchPendingAsync_WithNothingPending_DoesNothing()
	{
		await using var dbContext = CreateDbContext();

		var emailSender = new FakeEmailSender();
		var dispatcher = CreateDispatcher(dbContext, emailSender, CreateOptions());

		var processed = await dispatcher.DispatchPendingAsync(CancellationToken.None);

		Assert.Equal(0, processed);
		Assert.Empty(emailSender.SentRecipients);
	}

	[Fact]
	public async Task DispatchPendingAsync_DoesNotResendMessageAlreadySent()
	{
		await using var dbContext = CreateDbContext();
		AddPendingMessage(dbContext, recipient: "user@example.com");
		await dbContext.SaveChangesAsync();

		var alreadySent = await dbContext.EmailMessages.SingleAsync();
		alreadySent.MarkAsSent();
		await dbContext.SaveChangesAsync();

		var emailSender = new FakeEmailSender();
		var dispatcher = CreateDispatcher(dbContext, emailSender, CreateOptions());

		var processed = await dispatcher.DispatchPendingAsync(CancellationToken.None);

		Assert.Equal(0, processed);
		Assert.Empty(emailSender.SentRecipients);
	}

	[Fact]
	public async Task DispatchPendingAsync_WhenSendFails_RecordsAttemptAndKeepsMessagePending()
	{
		await using var dbContext = CreateDbContext();
		AddPendingMessage(dbContext, recipient: "falha@example.com");
		await dbContext.SaveChangesAsync();

		var emailSender = new FakeEmailSender { FailingRecipient = "falha@example.com" };
		var dispatcher = CreateDispatcher(dbContext, emailSender, CreateOptions());

		await dispatcher.DispatchPendingAsync(CancellationToken.None);

		var emailMessage = await dbContext.EmailMessages.SingleAsync();
		Assert.False(emailMessage.Sent);
		Assert.Equal(1, emailMessage.AttemptCount);
		Assert.Equal("servidor SMTP indisponivel", emailMessage.LastError);
	}

	[Fact]
	public async Task DispatchPendingAsync_WhenOneMessageFails_StillSendsTheOthers()
	{
		await using var dbContext = CreateDbContext();
		AddPendingMessage(dbContext, recipient: "falha@example.com");
		AddPendingMessage(dbContext, recipient: "ok@example.com");
		await dbContext.SaveChangesAsync();

		var emailSender = new FakeEmailSender { FailingRecipient = "falha@example.com" };
		var dispatcher = CreateDispatcher(dbContext, emailSender, CreateOptions());

		await dispatcher.DispatchPendingAsync(CancellationToken.None);

		Assert.Equal("ok@example.com", Assert.Single(emailSender.SentRecipients));

		var delivered = await dbContext.EmailMessages.SingleAsync(m => m.Recipient == "ok@example.com");
		Assert.True(delivered.Sent);
	}

	[Fact]
	public async Task DispatchPendingAsync_StopsRetryingAfterMaxAttempts()
	{
		await using var dbContext = CreateDbContext();
		AddPendingMessage(dbContext, recipient: "falha@example.com");
		await dbContext.SaveChangesAsync();

		var emailSender = new FakeEmailSender { FailingRecipient = "falha@example.com" };
		var dispatcher = CreateDispatcher(dbContext, emailSender, CreateOptions(maxAttempts: 2));

		Assert.Equal(1, await dispatcher.DispatchPendingAsync(CancellationToken.None));
		Assert.Equal(1, await dispatcher.DispatchPendingAsync(CancellationToken.None));

		// Terceira rodada: a mensagem ja esgotou as tentativas e sai da fila,
		// em vez de bater no servidor SMTP para sempre.
		Assert.Equal(0, await dispatcher.DispatchPendingAsync(CancellationToken.None));

		var emailMessage = await dbContext.EmailMessages.SingleAsync();
		Assert.Equal(2, emailMessage.AttemptCount);
		Assert.True(emailMessage.HasExhaustedAttempts(maxAttempts: 2));
	}

	[Fact]
	public async Task DispatchPendingAsync_RespectsBatchSize()
	{
		await using var dbContext = CreateDbContext();
		AddPendingMessage(dbContext, recipient: "um@example.com");
		AddPendingMessage(dbContext, recipient: "dois@example.com");
		AddPendingMessage(dbContext, recipient: "tres@example.com");
		await dbContext.SaveChangesAsync();

		var emailSender = new FakeEmailSender();
		var dispatcher = CreateDispatcher(dbContext, emailSender, CreateOptions(batchSize: 2));

		var processed = await dispatcher.DispatchPendingAsync(CancellationToken.None);

		Assert.Equal(2, processed);
		Assert.Equal(2, emailSender.SentRecipients.Count);
	}
}
