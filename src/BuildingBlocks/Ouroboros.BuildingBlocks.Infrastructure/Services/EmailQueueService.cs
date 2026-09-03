using Ouroboros.BuildingBlocks.Application;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure;

public sealed class EmailQueueService : IEmailQueueService
{
	private readonly AppDbContext _dbContext;

	public EmailQueueService(AppDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public async Task<long> EnqueueAsync(
		string subject,
		string bodyHtml,
		string recipient,
		CancellationToken cancellationToken
	)
	{
		var emailMessage = new EmailMessage(
			subject: subject,
			bodyHtml: bodyHtml,
			recipient: recipient
		);

		_dbContext.Set<EmailMessage>().Add(emailMessage);

		await _dbContext.SaveChangesAsync(cancellationToken);

		return emailMessage.Id;
	}
}
