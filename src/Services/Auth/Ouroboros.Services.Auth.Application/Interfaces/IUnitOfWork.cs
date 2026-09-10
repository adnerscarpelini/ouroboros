namespace Ouroboros.Services.Auth.Application;

public interface IUnitOfWork
{
	Task SaveChangesAsync(
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);

	// Roda a operação inteira dentro de uma transação: ou tudo é gravado, ou nada é. É o que mantém o
	// dado de negócio e a solicitação de notificação inseparáveis — sem isso, uma falha no meio deixaria
	// o banco num estado parcial, como um usuário criado sem token de confirmação.
	Task ExecuteInTransactionAsync(
		Func<CancellationToken, Task> operation,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);

	Task<TResult> ExecuteInTransactionAsync<TResult>(
		Func<CancellationToken, Task<TResult>> operation,
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);
}
