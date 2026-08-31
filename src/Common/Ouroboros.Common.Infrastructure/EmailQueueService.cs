using Ouroboros.Common.Application;
using Ouroboros.Common.Domain;

namespace Ouroboros.Common.Infrastructure;

public sealed class EmailQueueService : IEmailQueueService
{
	private readonly CommonDbContext _dbContext;

	public EmailQueueService(CommonDbContext dbContext)
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

		_dbContext.EmailMessages.Add(emailMessage);

		await _dbContext.SaveChangesAsync(cancellationToken);

		return emailMessage.Id;
	}
}
