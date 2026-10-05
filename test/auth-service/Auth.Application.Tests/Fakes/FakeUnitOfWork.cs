namespace Ouroboros.Auth.Application.Fakes;

using Ouroboros.Auth.Application.Gateways;

// Executa o delegate e registra se houve commit. Nao desfaz escritas: o rollback real e coberto na integracao.
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Commits { get; private set; }

    public int Rollbacks { get; private set; }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> work)
    {
        try
        {
            var result = await work();
            Commits++;
            return result;
        }
        catch
        {
            Rollbacks++;
            throw;
        }
    }

    public async Task ExecuteAsync(Func<Task> work)
    {
        await ExecuteAsync(async () =>
        {
            await work();
            return true;
        });
    }
}
