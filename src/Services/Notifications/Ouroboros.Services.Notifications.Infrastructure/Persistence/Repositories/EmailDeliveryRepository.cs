using Microsoft.EntityFrameworkCore;
using Ouroboros.Services.Notifications.Application;
using Ouroboros.Services.Notifications.Domain;

namespace Ouroboros.Services.Notifications.Infrastructure;

public sealed class EmailDeliveryRepository : IEmailDeliveryRepository
{
	private readonly NotificationsDbContext _dbContext;

	public EmailDeliveryRepository(NotificationsDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public void Add(EmailDelivery delivery)
	{
		_dbContext.EmailDeliveries.Add(delivery);
	}

	public Task<EmailDelivery?> GetByRequestAsync(
		string producer,
		Guid requestId,
		CancellationToken cancellationToken
	)
	{
		return _dbContext.EmailDeliveries
			.FirstOrDefaultAsync(
				delivery => delivery.Producer == producer && delivery.RequestId == requestId,
				cancellationToken
			);
	}

	public async Task<IReadOnlyCollection<EmailDelivery>> GetDueForDeliveryAsync(
		DateTime instant,
		int batchSize,
		CancellationToken cancellationToken
	)
	{
		return await _dbContext.EmailDeliveries
			.Where(delivery =>
				(delivery.Status == EmailDeliveryStatus.Pending || delivery.Status == EmailDeliveryStatus.RetryScheduled)
				&& (delivery.NextAttemptAt == null || delivery.NextAttemptAt <= instant))
			.OrderBy(delivery => delivery.Id)
			.Take(batchSize)
			.ToListAsync(cancellationToken);
	}
}
