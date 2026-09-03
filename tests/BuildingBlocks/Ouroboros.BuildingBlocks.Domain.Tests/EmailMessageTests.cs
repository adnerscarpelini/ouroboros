namespace Ouroboros.BuildingBlocks.Domain.Tests;

public class EmailMessageTests
{
	private static EmailMessage CreateEmailMessage()
	{
		return new EmailMessage(
			subject: "Bem-vindo",
			bodyHtml: "<p>Olá!</p>",
			recipient: "user@example.com"
		);
	}

	[Fact]
	public void Constructor_CreatesUnsentMessage()
	{
		var emailMessage = CreateEmailMessage();

		Assert.False(emailMessage.Sent);
		Assert.Null(emailMessage.SentAt);
		Assert.Equal(0, emailMessage.AttemptCount);
		Assert.Null(emailMessage.LastAttemptAt);
		Assert.Null(emailMessage.LastError);
	}

	[Fact]
	public void MarkAsSent_SetsSentAndSentAt()
	{
		var emailMessage = CreateEmailMessage();

		emailMessage.MarkAsSent();

		Assert.True(emailMessage.Sent);
		Assert.NotNull(emailMessage.SentAt);
		Assert.Equal(1, emailMessage.AttemptCount);
		Assert.NotNull(emailMessage.LastAttemptAt);
	}

	[Fact]
	public void MarkAsSent_AfterFailedAttempt_ClearsLastError()
	{
		var emailMessage = CreateEmailMessage();
		emailMessage.RegisterFailedAttempt("servidor fora do ar");

		emailMessage.MarkAsSent();

		Assert.True(emailMessage.Sent);
		Assert.Null(emailMessage.LastError);
		Assert.Equal(2, emailMessage.AttemptCount);
	}

	[Fact]
	public void RegisterFailedAttempt_IncrementsCountAndKeepsMessageUnsent()
	{
		var emailMessage = CreateEmailMessage();

		emailMessage.RegisterFailedAttempt("servidor fora do ar");

		Assert.False(emailMessage.Sent);
		Assert.Null(emailMessage.SentAt);
		Assert.Equal(1, emailMessage.AttemptCount);
		Assert.Equal("servidor fora do ar", emailMessage.LastError);
		Assert.NotNull(emailMessage.LastAttemptAt);
	}

	[Fact]
	public void RegisterFailedAttempt_WithVeryLongError_TruncatesIt()
	{
		var emailMessage = CreateEmailMessage();

		emailMessage.RegisterFailedAttempt(new string('x', 1000));

		Assert.Equal(500, emailMessage.LastError!.Length);
	}

	[Fact]
	public void HasExhaustedAttempts_OnlyAfterReachingTheLimit()
	{
		var emailMessage = CreateEmailMessage();

		emailMessage.RegisterFailedAttempt("erro");
		Assert.False(emailMessage.HasExhaustedAttempts(maxAttempts: 2));

		emailMessage.RegisterFailedAttempt("erro");
		Assert.True(emailMessage.HasExhaustedAttempts(maxAttempts: 2));
	}
}
