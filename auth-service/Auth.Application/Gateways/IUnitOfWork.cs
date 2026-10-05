namespace Ouroboros.Auth.Application.Gateways;

/// <summary>
/// Unidade de trabalho transacional: tudo que o delegate escrever vale junto ou nao vale.
/// Qualquer excecao desfaz as escritas e e relancada. Nao existe transacao aninhada.
/// </summary>
public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> work);

    Task ExecuteAsync(Func<Task> work);
}
