namespace Ouroboros.Auth.Infrastructure.Persistence;

using Ouroboros.Auth.Application.Gateways;

public sealed class SqlUnitOfWork : IUnitOfWork
{
    private readonly DbSession _session;

    public SqlUnitOfWork(DbSession session)
    {
        _session = session;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> work)
    {
        await _session.BeginAsync();

        try
        {
            var result = await work();
            await _session.CommitAsync();
            return result;
        }
        finally
        {
            await _session.EndAsync();
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
