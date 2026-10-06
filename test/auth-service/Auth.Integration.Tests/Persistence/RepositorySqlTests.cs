namespace Ouroboros.Auth.Integration.Tests.Persistence;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Ouroboros.Auth.Infrastructure.Persistence;
using Ouroboros.Auth.Infrastructure.Security;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Spec 2026092512: o SQL dos repositorios que os testes unitarios, com repositorio falso, nao alcancam.
[Collection(AuthApiCollection.Name)]
public sealed class RepositorySqlTests : IAsyncLifetime
{
    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;
    private readonly Sha256TokenGenerator _tokens = new();

    public RepositorySqlTests(AuthApiFixture fixture)
    {
        _fixture = fixture;
        _api = new TestApi(fixture);
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

    public Task DisposeAsync()
    {
        _api.Dispose();
        return Task.CompletedTask;
    }

    // Filtros de deleted_at

    [Fact]
    public async Task ShouldIgnoreDeletedUserInEveryLookup()
    {
        var user = await _api.CreateUserAsync("gone.user", "gone@example.com");
        await DeleteLogicallyAsync(user.ExternalId);

        var users = CreateUserRepository();

        Assert.Null(await users.GetByExternalIdAsync(user.ExternalId));
        Assert.Null(await users.GetByLoginAsync(user.NormalizedLogin));
        Assert.Null(await users.GetByEmailAsync(user.NormalizedEmail));
        Assert.Null(await users.GetByLoginOrEmailAsync(user.NormalizedLogin));
        Assert.Null(await users.GetByLoginOrEmailAsync(user.NormalizedEmail));
    }

    [Fact]
    public async Task ShouldReportDeletedLoginOnlyForDeletedUsers()
    {
        var gone = await _api.CreateUserAsync("gone.login");
        await _api.CreateUserAsync("live.login");
        await DeleteLogicallyAsync(gone.ExternalId);

        var users = CreateUserRepository();

        Assert.True(await users.ExistsDeletedByLoginAsync(gone.NormalizedLogin));
        Assert.False(await users.ExistsDeletedByLoginAsync("LIVE.LOGIN"));
        Assert.False(await users.ExistsDeletedByLoginAsync("NEVER.EXISTED"));
    }

    [Fact]
    public async Task ShouldCountOnlyActiveAndNotDeletedAdmins()
    {
        // Spec 2026092505: a contagem com lock so roda dentro de uma unidade de trabalho.
        var active = await _api.CreateUserAsync("admin.active");
        var inactive = await _api.CreateUserAsync("admin.inactive", confirmed: false);
        var deleted = await _api.CreateUserAsync("admin.deleted");
        var common = await _api.CreateUserAsync("common.user");

        foreach (var admin in new[] { active, inactive, deleted })
        {
            await _api.SetRoleAsync(admin.ExternalId, UserRole.Admin);
        }

        await DeleteLogicallyAsync(deleted.ExternalId);

        await using var session = _fixture.CreateSession();
        var count = await new SqlUnitOfWork(session).ExecuteAsync(() =>
            new DapperUserRepository(session, NullLogger<DapperUserRepository>.Instance).CountActiveAdminsForUpdateAsync());

        Assert.Equal(1, count);
        Assert.NotEqual(UserRole.Admin, (await CreateUserRepository().GetByExternalIdAsync(common.ExternalId))!.Role);
    }

    // Regra de hints (skill ouroboros-dba): leitura nao espera linha travada por outra transacao.
    // READPAST pula a linha travada (para quem le, ela some); NOLOCK le mesmo travada, inclusive dado nao commitado.

    [Fact]
    public async Task ShouldNotWaitAndShouldSkipTheRowWhenDecisionReadFindsItLockedByAnotherTransaction()
    {
        var locked = await _api.CreateUserAsync("readpast.locked");
        var free = await _api.CreateUserAsync("readpast.free");
        await using var writer = await LockUserRowAsync(locked.ExternalId);

        var started = DateTimeOffset.UtcNow;
        var lockedRead = await CreateUserRepository().GetByExternalIdAsync(locked.ExternalId);
        var freeRead = await CreateUserRepository().GetByExternalIdAsync(free.ExternalId);

        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(3), "A leitura esperou o lock em vez de pular a linha.");
        Assert.Null(lockedRead);
        Assert.NotNull(freeRead);
    }

    [Fact]
    public async Task ShouldSkipTokenRowLockedByAnotherTransactionOnLookupByHash()
    {
        var user = await _api.CreateUserAsync("readpast.token");
        var raw = await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);
        await using var writer = new SqlConnection(_fixture.ConnectionString);
        await writer.OpenAsync();
        await using var transaction = writer.BeginTransaction();
        await Dapper.SqlMapper.ExecuteAsync(
            writer,
            "UPDATE auth.tokens SET updated_at = SYSDATETIMEOFFSET() WHERE token_hash = @Hash;",
            new { Hash = _tokens.Hash(raw) },
            transaction);

        var token = await CreateTokenRepository().GetByHashAsync(_tokens.Hash(raw), TokenType.PasswordReset);

        // Consequencia conhecida do READPAST: enquanto outra transacao altera o token, quem le nao o ve.
        Assert.Null(token);
    }

    [Fact]
    public async Task ShouldNotWaitWhenInformativeReadFindsRowLockedByAnotherTransaction()
    {
        var gone = await _api.CreateUserAsync("nolock.deleted");
        await DeleteLogicallyAsync(gone.ExternalId);
        await using var writer = await LockUserRowAsync(gone.ExternalId);

        var started = DateTimeOffset.UtcNow;
        var exists = await CreateUserRepository().ExistsDeletedByLoginAsync(gone.NormalizedLogin);

        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(3), "A leitura esperou o lock.");
        Assert.True(exists);
    }

    // Spec 2026092505: a contagem de Admins ativos toma um lock que dura ate o fim da transacao.
    // O lock e o sp_getapplock, nao UPDLOCK/HOLDLOCK: o deadlock da versao antiga foi corrigido na spec 2026092510.

    [Fact]
    public async Task ShouldRefuseCountForUpdateOutsideUnitOfWork()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateUserRepository().CountActiveAdminsForUpdateAsync());
    }

    [Fact]
    public async Task ShouldCountOnlyActiveAndNotDeletedAdminsWhenCountingForUpdate()
    {
        var active = await _api.CreateUserAsync("lock.count.active");
        var deleted = await _api.CreateUserAsync("lock.count.deleted");
        await _api.CreateUserAsync("lock.count.common");
        await _api.SetRoleAsync(active.ExternalId, UserRole.Admin);
        await _api.SetRoleAsync(deleted.ExternalId, UserRole.Admin);
        await DeleteLogicallyAsync(deleted.ExternalId);

        await using var session = _fixture.CreateSession();
        var count = await new SqlUnitOfWork(session).ExecuteAsync(() =>
            new DapperUserRepository(session, NullLogger<DapperUserRepository>.Instance).CountActiveAdminsForUpdateAsync());

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ShouldMakeASecondCountWaitUntilTheFirstTransactionEnds()
    {
        var first = await _api.CreateUserAsync("lock.wait.one");
        var second = await _api.CreateUserAsync("lock.wait.two");
        await _api.SetRoleAsync(first.ExternalId, UserRole.Admin);
        await _api.SetRoleAsync(second.ExternalId, UserRole.Admin);

        await using var holder = _fixture.CreateSession();
        Task<int>? waiting = null;

        await new SqlUnitOfWork(holder).ExecuteAsync(async () =>
        {
            var held = await new DapperUserRepository(holder, NullLogger<DapperUserRepository>.Instance).CountActiveAdminsForUpdateAsync();
            Assert.Equal(2, held);

            // Outra conexao, em outra transacao: precisa esperar o lock da transacao acima.
            waiting = Task.Run(async () =>
            {
                await using var other = _fixture.CreateSession();

                return await new SqlUnitOfWork(other).ExecuteAsync(() =>
                    new DapperUserRepository(other, NullLogger<DapperUserRepository>.Instance).CountActiveAdminsForUpdateAsync());
            });

            await Task.Delay(750);

            Assert.False(waiting.IsCompleted, "A segunda contagem nao esperou o lock da primeira transacao.");
        });

        Assert.Equal(2, await waiting!.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    // Spec 2026092504: o desbloqueio vale mesmo com a conta bloqueada.

    [Fact]
    public async Task ShouldClearLockoutEvenWhenAccountIsLocked()
    {
        var user = await _api.CreateUserAsync("clear.locked");
        await _api.ExecuteAsync(
            "UPDATE auth.users SET access_failed_count = 4, lockout_end = DATEADD(MINUTE, 10, SYSDATETIMEOFFSET()) WHERE external_id = @Id;",
            new { Id = user.ExternalId });

        // O reset condicional nao age durante o bloqueio.
        Assert.False(await CreateUserRepository().TryResetFailedAccessAsync(user.ExternalId, DateTimeOffset.UtcNow));

        await CreateUserRepository().ClearLockoutAsync(user.ExternalId, DateTimeOffset.UtcNow);

        var stored = (await CreateUserRepository().GetByExternalIdAsync(user.ExternalId))!;

        Assert.Equal(0, stored.AccessFailedCount);
        Assert.Null(stored.LockoutEnd);
    }

    [Fact]
    public async Task ShouldNotTouchDeletedUserWhenClearingLockout()
    {
        var user = await _api.CreateUserAsync("clear.deleted");
        await _api.ExecuteAsync(
            "UPDATE auth.users SET access_failed_count = 4, deleted_at = SYSDATETIMEOFFSET(), active = 0 WHERE external_id = @Id;",
            new { Id = user.ExternalId });

        await CreateUserRepository().ClearLockoutAsync(user.ExternalId, DateTimeOffset.UtcNow);

        Assert.Equal(4, await _api.QueryAsync<int>("SELECT access_failed_count FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId }));
    }

    // Indices unicos

    [Fact]
    public async Task ShouldAllowSameEmailAfterAccountIsDeleted()
    {
        var first = await _api.CreateUserAsync("email.first", "shared@example.com");
        await DeleteLogicallyAsync(first.ExternalId);

        await _api.CreateUserAsync("email.second", "shared@example.com");

        Assert.NotNull(await CreateUserRepository().GetByEmailAsync("SHARED@EXAMPLE.COM"));
    }

    [Fact]
    public async Task ShouldKeepLoginReservedAfterAccountIsDeleted()
    {
        var first = await _api.CreateUserAsync("login.reserved", "first@example.com");
        await DeleteLogicallyAsync(first.ExternalId);

        await Assert.ThrowsAsync<DuplicateLoginException>(() => _api.CreateUserAsync("login.reserved", "second@example.com"));
    }

    // ON DELETE CASCADE

    [Fact]
    public async Task ShouldRemoveTokensAndRefreshTokensWhenAbandonedUserIsRemoved()
    {
        var user = await _api.CreateUserAsync("abandoned", confirmed: false);
        await _api.AddTokenAsync(user.ExternalId, TokenType.EmailConfirmation);
        await AddRefreshTokenAsync(user.ExternalId);

        await CreateUserRepository().RemoveAsync(user.ExternalId);

        Assert.Equal(0, await _api.CountUsersAsync());
        Assert.Equal(0, await _api.QueryAsync<int>("SELECT COUNT(*) FROM auth.tokens;"));
        Assert.Equal(0, await _api.QueryAsync<int>("SELECT COUNT(*) FROM auth.refresh_tokens;"));
    }

    [Fact]
    public async Task ShouldNeverRemoveLogicallyDeletedUser()
    {
        var user = await _api.CreateUserAsync("kept.deleted");
        await DeleteLogicallyAsync(user.ExternalId);

        await CreateUserRepository().RemoveAsync(user.ExternalId);

        Assert.Equal(1, await _api.CountUsersAsync());
    }

    // Escritas condicionais

    [Fact]
    public async Task ShouldRevokeRefreshTokenOnlyOnce()
    {
        var user = await _api.CreateUserAsync("revoke.once");
        var hash = await AddRefreshTokenAsync(user.ExternalId);
        var first = (await CreateRefreshTokenRepository().GetByHashAsync(hash))!;
        var second = (await CreateRefreshTokenRepository().GetByHashAsync(hash))!;
        first.Revoke(DateTimeOffset.UtcNow);
        second.Revoke(DateTimeOffset.UtcNow);

        var firstResult = await CreateRefreshTokenRepository().TryRevokeAsync(first);
        var secondResult = await CreateRefreshTokenRepository().TryRevokeAsync(second);

        Assert.True(firstResult);
        Assert.False(secondResult);
    }

    [Fact]
    public async Task ShouldRevokeRefreshTokenInExactlyOneOfManyConcurrentCalls()
    {
        var user = await _api.CreateUserAsync("revoke.race");
        var hash = await AddRefreshTokenAsync(user.ExternalId);

        // Todas as instancias sao lidas antes: so a escrita disputa, que e o que o SQL condicional precisa resolver.
        var tokens = new List<RefreshToken>();

        for (var index = 0; index < 8; index++)
        {
            var token = (await CreateRefreshTokenRepository().GetByHashAsync(hash))!;
            token.Revoke(DateTimeOffset.UtcNow);
            tokens.Add(token);
        }

        var results = await Task.WhenAll(tokens.Select(token => CreateRefreshTokenRepository().TryRevokeAsync(token)));

        Assert.Single(results, won => won);
    }

    [Fact]
    public async Task ShouldMarkTokenAsUsedOnlyOnce()
    {
        var user = await _api.CreateUserAsync("mark.once");
        var raw = await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);
        var first = (await CreateTokenRepository().GetByHashAsync(_tokens.Hash(raw), TokenType.PasswordReset))!;
        var second = (await CreateTokenRepository().GetByHashAsync(_tokens.Hash(raw), TokenType.PasswordReset))!;
        first.MarkAsUsed(DateTimeOffset.UtcNow);
        second.MarkAsUsed(DateTimeOffset.UtcNow);

        var firstResult = await CreateTokenRepository().TryMarkAsUsedAsync(first);
        var secondResult = await CreateTokenRepository().TryMarkAsUsedAsync(second);

        Assert.True(firstResult);
        Assert.False(secondResult);
    }

    [Fact]
    public async Task ShouldMarkTokenAsUsedInExactlyOneOfManyConcurrentCalls()
    {
        var user = await _api.CreateUserAsync("mark.race");
        var raw = await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);

        var tokens = new List<Token>();

        for (var index = 0; index < 8; index++)
        {
            var token = (await CreateTokenRepository().GetByHashAsync(_tokens.Hash(raw), TokenType.PasswordReset))!;
            token.MarkAsUsed(DateTimeOffset.UtcNow);
            tokens.Add(token);
        }

        var results = await Task.WhenAll(tokens.Select(token => CreateTokenRepository().TryMarkAsUsedAsync(token)));

        Assert.Single(results, won => won);
    }

    // Spec 2026092503: o prazo e conferido no proprio UPDATE.

    [Fact]
    public async Task ShouldNotMarkExpiredTokenAsUsed()
    {
        var user = await _api.CreateUserAsync("mark.expired");
        var raw = await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);
        var token = (await CreateTokenRepository().GetByHashAsync(_tokens.Hash(raw), TokenType.PasswordReset))!;
        await _api.ExpireTokenAsync(raw);
        token.MarkAsUsed(DateTimeOffset.UtcNow);

        var result = await CreateTokenRepository().TryMarkAsUsedAsync(token);

        Assert.False(result);
        Assert.Null((await CreateTokenRepository().GetByHashAsync(_tokens.Hash(raw), TokenType.PasswordReset))!.UsedAt);
    }

    [Fact]
    public async Task ShouldNotMarkTokenAsUsedAfterItWasInvalidated()
    {
        var user = await _api.CreateUserAsync("mark.invalidated");
        var raw = await _api.AddTokenAsync(user.ExternalId, TokenType.EmailConfirmation);
        var token = (await CreateTokenRepository().GetByHashAsync(_tokens.Hash(raw), TokenType.EmailConfirmation))!;
        token.MarkAsUsed(DateTimeOffset.UtcNow.AddSeconds(1));

        await CreateTokenRepository().InvalidatePendingByUserAsync(user.ExternalId, TokenType.EmailConfirmation, DateTimeOffset.UtcNow);

        Assert.False(await CreateTokenRepository().TryMarkAsUsedAsync(token));
    }

    [Fact]
    public async Task ShouldRevokeOnlyActiveRefreshTokensOfTheUser()
    {
        var user = await _api.CreateUserAsync("revoke.all");
        var other = await _api.CreateUserAsync("revoke.other");
        await AddRefreshTokenAsync(user.ExternalId);
        await AddRefreshTokenAsync(user.ExternalId);
        var expired = await AddRefreshTokenAsync(user.ExternalId);
        await AddRefreshTokenAsync(other.ExternalId);
        await _api.ExecuteAsync(
            "UPDATE auth.refresh_tokens SET expires_at = DATEADD(MINUTE, -1, SYSDATETIMEOFFSET()) WHERE token_hash = @Hash;",
            new { Hash = expired });

        await CreateRefreshTokenRepository().RevokeAllActiveByUserAsync(user.ExternalId, DateTimeOffset.UtcNow);

        Assert.Equal(0, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
        Assert.Equal(1, await _api.CountActiveRefreshTokensAsync(other.ExternalId));
        Assert.Null((await CreateRefreshTokenRepository().GetByHashAsync(expired))!.RevokedAt);
    }

    [Fact]
    public async Task ShouldInvalidateOnlyPendingTokensOfTheTypeAndUser()
    {
        var user = await _api.CreateUserAsync("invalidate.user");
        var other = await _api.CreateUserAsync("invalidate.other");
        var pending = await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);
        var used = await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);
        var otherType = await _api.AddTokenAsync(user.ExternalId, TokenType.EmailConfirmation);
        var otherUser = await _api.AddTokenAsync(other.ExternalId, TokenType.PasswordReset);
        await _api.ExecuteAsync("UPDATE auth.tokens SET used_at = SYSDATETIMEOFFSET() WHERE token_hash = @Hash;", new { Hash = _tokens.Hash(used) });

        await CreateTokenRepository().InvalidatePendingByUserAsync(user.ExternalId, TokenType.PasswordReset, DateTimeOffset.UtcNow);

        var now = DateTimeOffset.UtcNow;
        var repository = CreateTokenRepository();

        Assert.False((await repository.GetByHashAsync(_tokens.Hash(pending), TokenType.PasswordReset))!.IsPending(now));
        Assert.True((await repository.GetByHashAsync(_tokens.Hash(otherType), TokenType.EmailConfirmation))!.IsPending(now));
        Assert.True((await repository.GetByHashAsync(_tokens.Hash(otherUser), TokenType.PasswordReset))!.IsPending(now));
    }

    [Fact]
    public async Task ShouldFindPendingTokenOnlyWhenNotUsedNotExpiredAndOfTheType()
    {
        var user = await _api.CreateUserAsync("pending.check");
        var repository = CreateTokenRepository();
        var now = DateTimeOffset.UtcNow;

        Assert.False(await repository.ExistsPendingByUserAsync(user.ExternalId, TokenType.EmailConfirmation, now));

        var raw = await _api.AddTokenAsync(user.ExternalId, TokenType.EmailConfirmation);

        Assert.True(await repository.ExistsPendingByUserAsync(user.ExternalId, TokenType.EmailConfirmation, now));
        Assert.False(await repository.ExistsPendingByUserAsync(user.ExternalId, TokenType.PasswordReset, now));

        await _api.ExpireTokenAsync(raw);

        Assert.False(await repository.ExistsPendingByUserAsync(user.ExternalId, TokenType.EmailConfirmation, now));
    }

    // Transacao aberta que mantem um lock de escrita na linha do usuario. O descarte faz o rollback e solta o lock.
    private async Task<LockedRow> LockUserRowAsync(Guid externalId)
    {
        var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        var transaction = connection.BeginTransaction();

        await Dapper.SqlMapper.ExecuteAsync(
            connection,
            "UPDATE auth.users SET full_name = 'locked-by-test' WHERE external_id = @Id;",
            new { Id = externalId },
            transaction);

        return new LockedRow(connection, transaction);
    }

    private sealed class LockedRow(SqlConnection connection, SqlTransaction transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private DapperUserRepository CreateUserRepository() =>
        new(_fixture.CreateSession(), NullLogger<DapperUserRepository>.Instance);

    private DapperTokenRepository CreateTokenRepository() => new(_fixture.CreateSession());

    private DapperRefreshTokenRepository CreateRefreshTokenRepository() => new(_fixture.CreateSession());

    private Task DeleteLogicallyAsync(Guid externalId) =>
        _api.ExecuteAsync(
            "UPDATE auth.users SET deleted_at = SYSDATETIMEOFFSET(), active = 0 WHERE external_id = @Id;",
            new { Id = externalId });

    // Devolve o hash gravado, que e a chave de busca do repositorio.
    private async Task<string> AddRefreshTokenAsync(Guid userExternalId)
    {
        var hash = _tokens.Hash(_tokens.Generate());
        var token = RefreshToken.Create(userExternalId, hash, DateTimeOffset.UtcNow.AddDays(1));

        await CreateRefreshTokenRepository().AddAsync(token);

        return hash;
    }
}
