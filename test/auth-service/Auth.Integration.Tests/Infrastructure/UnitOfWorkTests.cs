namespace Ouroboros.Auth.Integration.Tests.Infrastructure;

using Dapper;
using Microsoft.Data.SqlClient;
using Ouroboros.Auth.Infrastructure.Persistence;
using Xunit;

// Spec 2026092516: commit, rollback, transacao aninhada e autocommit contra o SQL Server real.
[Collection(AuthApiCollection.Name)]
public sealed class UnitOfWorkTests : IAsyncLifetime
{
    private readonly AuthApiFixture _fixture;

    public UnitOfWorkTests(AuthApiFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ShouldPersistBothWritesWhenWorkCompletes()
    {
        await using var session = _fixture.CreateSession();
        var unitOfWork = new SqlUnitOfWork(session);

        await unitOfWork.ExecuteAsync(async () =>
        {
            await InsertUserAsync(session, "uow.commit.1");
            await InsertUserAsync(session, "uow.commit.2");
        });

        Assert.Equal(2, await CountUsersAsync());
    }

    [Fact]
    public async Task ShouldRollBackBothWritesWhenWorkThrows()
    {
        await using var session = _fixture.CreateSession();
        var unitOfWork = new SqlUnitOfWork(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteAsync(async () =>
        {
            await InsertUserAsync(session, "uow.rollback.1");
            await InsertUserAsync(session, "uow.rollback.2");
            throw new InvalidOperationException("forced failure");
        }));

        Assert.Equal(0, await CountUsersAsync());
        Assert.False(session.InTransaction);
    }

    [Fact]
    public async Task ShouldThrowWhenUnitOfWorkIsNested()
    {
        await using var session = _fixture.CreateSession();
        var unitOfWork = new SqlUnitOfWork(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteAsync(async () =>
        {
            await InsertUserAsync(session, "uow.nested");
            await unitOfWork.ExecuteAsync(() => Task.CompletedTask);
        }));

        Assert.Equal(0, await CountUsersAsync());
    }

    [Fact]
    public async Task ShouldAutocommitWhenOutsideUnitOfWork()
    {
        await using var session = _fixture.CreateSession();

        await InsertUserAsync(session, "uow.autocommit");

        Assert.Equal(1, await CountUsersAsync());
    }

    [Fact]
    public async Task ShouldAllowAnotherUnitOfWorkAfterRollback()
    {
        await using var session = _fixture.CreateSession();
        var unitOfWork = new SqlUnitOfWork(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteAsync(() => throw new InvalidOperationException("forced failure")));

        await unitOfWork.ExecuteAsync(() => InsertUserAsync(session, "uow.after.rollback"));

        Assert.Equal(1, await CountUsersAsync());
    }

    private static Task InsertUserAsync(DbSession session, string login)
    {
        const string sql = """
            INSERT INTO auth.users (external_id, created_at, login, full_name, email, email_confirmed, password_hash, password_changed_at, active, role)
            VALUES (NEWID(), SYSDATETIMEOFFSET(), @Login, 'Uow Test', @Email, 0, 'hash', SYSDATETIMEOFFSET(), 0, 'User');
            """;

        return session.ExecuteAsync(sql, new { Login = login, Email = $"{login}@example.com" });
    }

    private async Task<int> CountUsersAsync()
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM auth.users;");
    }
}
