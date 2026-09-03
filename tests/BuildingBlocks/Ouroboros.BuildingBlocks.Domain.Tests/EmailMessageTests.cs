namespace Ouroboros.BuildingBlocks.Domain.Tests;

public class EmailMessageTests
{
	[Fact]
	public void Constructor_CreatesUnsentMessage()
	{
		var emailMessage = new EmailMessage(
			subject: "Bem-vindo",
			bodyHtml: "<p>Olá!</p>",
			recipient: "user@example.com"
		);

		Assert.False(emailMessage.Sent);
		Assert.Null(emailMessage.SentAt);
	}

	[Fact]
	public void MarkAsSent_SetsSentAndSentAt()
	{
		var emailMessage = new EmailMessage(
			subject: "Bem-vindo",
			bodyHtml: "<p>Olá!</p>",
			recipient: "user@example.com"
		);

		emailMessage.MarkAsSent();

		Assert.True(emailMessage.Sent);
		Assert.NotNull(emailMessage.SentAt);
	}
}
