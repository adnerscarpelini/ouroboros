using Ouroboros.NotificationsService.Application;
using Ouroboros.NotificationsService.Domain;

namespace Ouroboros.NotificationsService.Infrastructure.Tests;

public sealed class InMemoryEmailDeliveryRepository : IEmailDeliveryRepository
{
	private readonly List<EmailDelivery> _deliveries = [];

	public void Add(EmailDelivery delivery)
	{
		_deliveries.Add(delivery);
	}

	public void Update(EmailDelivery delivery)
	{
	}

	public Task<EmailDelivery?> GetByRequestAsync(
		string producer,
		Guid requestId,
		CancellationToken cancellationToken
	)
	{
		return Task.FromResult(_deliveries.FirstOrDefault(delivery =>
			delivery.Producer == producer && delivery.RequestId == requestId));
	}

	public Task<IReadOnlyCollection<EmailDelivery>> GetDueForDeliveryAsync(
		DateTime instant,
		int batchSize,
		CancellationToken cancellationToken
	)
	{
		IReadOnlyCollection<EmailDelivery> due = _deliveries
			.Where(delivery => delivery.IsDueAt(instant))
			.Take(batchSize)
			.ToList();

		return Task.FromResult(due);
	}

	// Antecipa a espera calculada pelo dispatcher, para exercitar várias rodadas de retentativa sem
	// depender do relógio.
	public void ClearRetrySchedule()
	{
		foreach (var delivery in _deliveries.Where(delivery => delivery.Status == EmailDeliveryStatus.RetryScheduled))
		{
			delivery.ScheduleRetry(
				attempt: delivery.Attempts.Last(),
				error: delivery.LastError ?? string.Empty,
				nextAttemptAt: DateTime.UtcNow.AddSeconds(-1)
			);
		}
	}
}
