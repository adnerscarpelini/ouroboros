using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ouroboros.BuildingBlocks.Application;
namespace Ouroboros.BuildingBlocks.Infrastructure;

public sealed class OutboxPublisherProcessorService : BackgroundService
{
	private readonly IServiceScopeFactory _scope;
	private readonly OutboxOptions _options;
	private readonly ILogger<OutboxPublisherProcessorService> _logger;

	public OutboxPublisherProcessorService(
		IServiceScopeFactory scope,
		OutboxOptions options,
		ILogger<OutboxPublisherProcessorService> logger
	)
	{
		_scope = scope;
		_options = options;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		using var timer = new PeriodicTimer(_options.PollingInterval);

		try
		{
			do
			{
				try
				{
					await using var scope = _scope.CreateAsyncScope();
					var publisher = scope.ServiceProvider.GetRequiredService<OutboxPublisherService>();

					await publisher.PublishPendingAsync(cancellationToken);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					_logger.LogError(ex, "Falha ao publicar a outbox.");
				}
			}
			while (await timer.WaitForNextTickAsync(cancellationToken));
		}
		catch (OperationCanceledException)
		{
		}
	}
}
