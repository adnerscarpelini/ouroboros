namespace Ouroboros.NotificationsService.Application;

public interface IUnitOfWork
{
	Task SaveChangesAsync(
		// Cancela a operação em andamento se a aplicação estiver sendo encerrada.
		CancellationToken cancellationToken
	);
}
