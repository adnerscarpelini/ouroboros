using Ouroboros.Services.Notifications.Application;
using Ouroboros.Services.Notifications.Domain;

namespace Ouroboros.Services.Notifications.Application.Tests;

public sealed class FakeEmailDeliveryRepository : IEmailDeliveryRepository
{
	private readonly List<EmailDelivery> _deliveries = [];

	public IReadOnlyList<EmailDelivery> Deliveries => _deliveries;

	public void Add(EmailDelivery delivery)
	{
		_deliveries.Add(delivery);
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
}
