namespace Ouroboros.Services.Auth.Application;

public interface IUnitOfWork
{
	Task SaveChangesAsync(
		// Cancela a operação em andamento se o request HTTP for encerrado antes de terminar (ex.: cliente desconectou).
		CancellationToken cancellationToken
	);

	// Roda a operação inteira dentro de uma transação: ou tudo é gravado, ou nada é. Necessário porque
	// um caso de uso pode precisar de mais de um SaveChanges (ex.: gravar a mensagem de e-mail antes de
	// criar o Token que aponta pra ela, já que o id da mensagem só existe depois de gravada). Sem isso,
	// uma falha no meio deixaria o banco num estado parcial — um usuário criado sem token de confirmação.
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
