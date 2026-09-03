using Microsoft.EntityFrameworkCore;

namespace Ouroboros.BuildingBlocks.Infrastructure.Tests;

public class EmailQueueServiceTests
{
	private static TestDbContext CreateDbContext()
	{
		var options = new DbContextOptionsBuilder<TestDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		return new TestDbContext(options);
	}

	[Fact]
	public async Task EnqueueAsync_CreatesUnsentEmailMessage()
	{
		await using var dbContext = CreateDbContext();
		var emailQueueService = new EmailQueueService(dbContext);

		var id = await emailQueueService.EnqueueAsync(
			subject: "Confirme seu cadastro",
			bodyHtml: "<p>Olá!</p>",
			recipient: "user@example.com",
			cancellationToken: CancellationToken.None
		);

		var emailMessage = await dbContext.EmailMessages.SingleAsync();
		Assert.Equal(id, emailMessage.Id);
		Assert.Equal("Confirme seu cadastro", emailMessage.Subject);
		Assert.Equal("user@example.com", emailMessage.Recipient);
		Assert.False(emailMessage.Sent);
		Assert.Null(emailMessage.SentAt);
	}
}
