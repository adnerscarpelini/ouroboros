namespace Ouroboros.Auth.Integration.Tests.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Application.UseCases.CleanupExpiredTokens;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Infrastructure.Persistence;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Spec 2026092515: limpeza de tokens expirados contra o banco real.
[Collection(AuthApiCollection.Name)]
public sealed class TokenCleanupTests : IAsyncLifetime
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public TokenCleanupTests(AuthApiFixture fixture)
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

    [Fact]
    public async Task ShouldDeleteRowsExpiredForMoreThanTheRetentionAndKeepEverythingElse()
    {
        var user = await _api.CreateUserAsync("cleanup.user");
        await SeedAsync(user, "old", expiresDaysFromNow: -31);
        await SeedAsync(user, "recent", expiresDaysFromNow: -10);
        await SeedAsync(user, "active", expiresDaysFromNow: 5);
        await SeedAsync(user, "used-active", expiresDaysFromNow: 5, usedOrRevoked: true);
        await SeedAsync(user, "used-old", expiresDaysFromNow: -45, usedOrRevoked: true);

        var response = await CreateInteractor().ExecuteAsync(new CleanupExpiredTokensRequest(Retention));

        Assert.Equal(new CleanupExpiredTokensResponse(2, 2), response);
        Assert.Equal(["active", "recent", "used-active"], await RemainingAsync("auth.tokens"));
        Assert.Equal(["active", "recent", "used-active"], await RemainingAsync("auth.refresh_tokens"));
    }

    [Fact]
    public async Task ShouldKeepRevokedRefreshTokensThatHaveNotExpiredYetBecauseReuseDetectionNeedsThem()
    {
        var user = await _api.CreateUserAsync("cleanup.revoked");
        await SeedAsync(user, "revoked", expiresDaysFromNow: 3, usedOrRevoked: true);

        await CreateInteractor().ExecuteAsync(new CleanupExpiredTokensRequest(Retention));

        Assert.Equal(["revoked"], await RemainingAsync("auth.refresh_tokens"));
    }

    [Fact]
    public async Task ShouldProcessMoreRowsThanOneBatchInFull()
    {
        var user = await _api.CreateUserAsync("cleanup.volume");
        await SeedManyAsync(user, count: 2_500, expiresDaysFromNow: -60);
        await SeedAsync(user, "keep", expiresDaysFromNow: 1);

        var response = await CreateInteractor().ExecuteAsync(new CleanupExpiredTokensRequest(Retention));

        Assert.Equal(new CleanupExpiredTokensResponse(2_500, 2_500), response);
        Assert.Equal(["keep"], await RemainingAsync("auth.tokens"));
        Assert.Equal(["keep"], await RemainingAsync("auth.refresh_tokens"));
    }

    [Fact]
    public async Task ShouldFinishWithoutErrorWhenTwoCleanupsRunAtTheSameTime()
    {
        var user = await _api.CreateUserAsync("cleanup.concurrent");
        await SeedManyAsync(user, count: 3_000, expiresDaysFromNow: -60);
        await SeedAsync(user, "keep-active", expiresDaysFromNow: 2);
        await SeedAsync(user, "keep-recent", expiresDaysFromNow: -5);

        var responses = await Task.WhenAll(
            CreateInteractor().ExecuteAsync(new CleanupExpiredTokensRequest(Retention)),
            CreateInteractor().ExecuteAsync(new CleanupExpiredTokensRequest(Retention)));

        // Cada linha e apagada uma vez so: a soma das duas execucoes e o total expirado.
        Assert.Equal(3_000, responses.Sum(response => response.Tokens));
        Assert.Equal(3_000, responses.Sum(response => response.RefreshTokens));
        Assert.Equal(["keep-active", "keep-recent"], await RemainingAsync("auth.tokens"));
        Assert.Equal(["keep-active", "keep-recent"], await RemainingAsync("auth.refresh_tokens"));
    }

    [Fact]
    public async Task ShouldNotTouchUsersOrOtherUsersActiveTokens()
    {
        var user = await _api.CreateUserAsync("cleanup.owner");
        var other = await _api.CreateUserAsync("cleanup.other");
        await SeedAsync(user, "old", expiresDaysFromNow: -90);
        await SeedAsync(other, "other-active", expiresDaysFromNow: 4);

        await CreateInteractor().ExecuteAsync(new CleanupExpiredTokensRequest(Retention));

        Assert.Equal(2, await _api.CountUsersAsync());
        Assert.Equal(["other-active"], await RemainingAsync("auth.tokens"));
    }

    [Fact]
    public async Task ShouldCleanInTheBackgroundAndLogOnlyCountsWhenTheServiceIsEnabled()
    {
        var user = await _api.CreateUserAsync("cleanup.background");
        await SeedAsync(user, "old-bg", expiresDaysFromNow: -40);
        await SeedAsync(user, "active-bg", expiresDaysFromNow: 6);
        var logs = new CapturingLoggerProvider();
        using var factory = CreateEnabledFactory(logs);

        using var client = factory.CreateClient();
        await WaitUntilAsync(async () => (await RemainingAsync("auth.tokens")).SequenceEqual(["active-bg"]));

        Assert.Equal(["active-bg"], await RemainingAsync("auth.tokens"));
        Assert.Equal(["active-bg"], await RemainingAsync("auth.refresh_tokens"));

        // O log sai logo depois do DELETE: espera por ele. Com intervalo de 1 s pode haver mais de um ciclo, e o primeiro
        // apagou 1 linha de cada tabela.
        await WaitUntilAsync(() => Task.FromResult(logs.Messages.Contains("Expired token cleanup removed 1 tokens and 1 refresh tokens")));

        Assert.Contains("Expired token cleanup removed 1 tokens and 1 refresh tokens", logs.Messages);
        Assert.DoesNotContain(logs.Messages, entry => entry.Contains("old-bg") || entry.Contains("active-bg"));
    }

    [Fact]
    public async Task ShouldLogErrorAndTryAgainInTheNextCycleWhenACycleFails()
    {
        var user = await _api.CreateUserAsync("cleanup.failure");
        await SeedAsync(user, "old-fail", expiresDaysFromNow: -40);
        var logs = new CapturingLoggerProvider();
        FailingOnceTokenRepository.Reset();
        using var factory = CreateEnabledFactory(logs, services =>
        {
            services.RemoveAll<ITokenRepository>();
            services.AddScoped<ITokenRepository>(provider =>
                new FailingOnceTokenRepository(ActivatorUtilities.CreateInstance<DapperTokenRepository>(provider)));
        });

        using var client = factory.CreateClient();
        await WaitUntilAsync(async () => (await RemainingAsync("auth.tokens")).Count == 0);
        await WaitUntilAsync(() => Task.FromResult(logs.Messages.Any(entry => entry.StartsWith("Expired token cleanup removed"))));

        Assert.Empty(await RemainingAsync("auth.tokens"));
        Assert.Contains(logs.Messages, entry => entry.StartsWith("Expired token cleanup failed"));
        Assert.Contains(logs.Messages, entry => entry.StartsWith("Expired token cleanup removed"));
    }

    [Theory]
    [InlineData("TokenCleanup:Interval", "00:00:00")]
    [InlineData("TokenCleanup:Retention", "00:00:00")]
    [InlineData("TokenCleanup:Retention", "-1.00:00:00")]
    public void ShouldFailAtStartupWhenTheCleanupSettingsAreInvalid(
        string key,
        string value)
    {
        using var factory = _fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(key, error.Message);
    }

    [Fact]
    public void ShouldEnableTheCleanupByDefaultWithHourlyIntervalAndThirtyDaysOfRetention()
    {
        // Le o appsettings.json direto: subir o host com a limpeza ligada deixaria um ciclo rodando em segundo plano.
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
        var settings = document.RootElement.GetProperty("TokenCleanup");

        Assert.True(settings.GetProperty("Enabled").GetBoolean());
        Assert.Equal(TimeSpan.FromHours(1), TimeSpan.Parse(settings.GetProperty("Interval").GetString()!));
        Assert.Equal(TimeSpan.FromDays(30), TimeSpan.Parse(settings.GetProperty("Retention").GetString()!));
    }

    private CleanupExpiredTokensInteractor CreateInteractor()
    {
        var session = _fixture.CreateSession();

        return new CleanupExpiredTokensInteractor(new DapperTokenRepository(session), new DapperRefreshTokenRepository(session));
    }

    private WebApplicationFactory<Program> CreateEnabledFactory(
        CapturingLoggerProvider logs,
        Action<IServiceCollection>? configureServices = null)
    {
        return _fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("TokenCleanup:Enabled", "true");
            builder.UseSetting("TokenCleanup:Interval", "00:00:01");
            builder.ConfigureServices(services =>
            {
                // O UseSerilog do Program ignora outros providers: troca-se a ILoggerFactory.
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(new CapturingLoggerFactory(logs));
                configureServices?.Invoke(services);
            });
        });
    }

    // Uma linha em cada tabela, identificada pelo prefixo do token_hash.
    private async Task SeedAsync(
        User user,
        string label,
        int expiresDaysFromNow,
        bool usedOrRevoked = false)
    {
        const string sql = """
            DECLARE @userId bigint = (SELECT id FROM auth.users WHERE external_id = @ExternalId);
            DECLARE @expiresAt datetimeoffset = DATEADD(DAY, @Days, SYSDATETIMEOFFSET());
            DECLARE @flagAt datetimeoffset = CASE WHEN @Flag = 1 THEN DATEADD(DAY, -1, SYSDATETIMEOFFSET()) END;

            INSERT INTO auth.tokens (external_id, created_at, user_id, type, token_hash, expires_at, used_at)
            VALUES (NEWID(), SYSDATETIMEOFFSET(), @userId, N'PasswordReset', @Label, @expiresAt, @flagAt);

            INSERT INTO auth.refresh_tokens (external_id, created_at, user_id, session_id, token_hash, expires_at, revoked_at)
            VALUES (NEWID(), SYSDATETIMEOFFSET(), @userId, NEWID(), @Label, @expiresAt, @flagAt);
            """;

        await _api.ExecuteAsync(sql, new { user.ExternalId, Days = expiresDaysFromNow, Label = label, Flag = usedOrRevoked ? 1 : 0 });
    }

    private async Task SeedManyAsync(
        User user,
        int count,
        int expiresDaysFromNow)
    {
        const string sql = """
            DECLARE @userId bigint = (SELECT id FROM auth.users WHERE external_id = @ExternalId);
            DECLARE @expiresAt datetimeoffset = DATEADD(DAY, @Days, SYSDATETIMEOFFSET());

            WITH numbers AS (
                SELECT TOP (@Count) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
                FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b
            )
            INSERT INTO auth.tokens (external_id, created_at, user_id, type, token_hash, expires_at)
            SELECT NEWID(), SYSDATETIMEOFFSET(), @userId, N'EmailConfirmation', CONCAT(N'bulk-', n), @expiresAt FROM numbers;

            WITH numbers AS (
                SELECT TOP (@Count) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
                FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b
            )
            INSERT INTO auth.refresh_tokens (external_id, created_at, user_id, session_id, token_hash, expires_at)
            SELECT NEWID(), SYSDATETIMEOFFSET(), @userId, NEWID(), CONCAT(N'bulk-', n), @expiresAt FROM numbers;
            """;

        await _api.ExecuteAsync(sql, new { user.ExternalId, Days = expiresDaysFromNow, Count = count });
    }

    private async Task<List<string>> RemainingAsync(string table)
    {
        var hashes = await _api.QueryListAsync<string>($"SELECT token_hash FROM {table} ORDER BY token_hash;");

        return hashes;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(200);
        }
    }

    // Falha so na primeira chamada de DeleteExpiredBatchAsync (valendo pro processo, nao por instancia).
    private sealed class FailingOnceTokenRepository : ITokenRepository
    {
        private static int _calls;

        private readonly ITokenRepository _inner;

        public FailingOnceTokenRepository(ITokenRepository inner)
        {
            _inner = inner;
        }

        public static void Reset() => Interlocked.Exchange(ref _calls, 0);

        public Task<int> DeleteExpiredBatchAsync(
            DateTimeOffset expiredBefore,
            int batchSize)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                throw new InvalidOperationException("Injected cleanup failure.");
            }

            return _inner.DeleteExpiredBatchAsync(expiredBefore, batchSize);
        }

        public Task AddAsync(Token token) => _inner.AddAsync(token);

        public Task<Token?> GetByHashAsync(
            string tokenHash,
            TokenType type) => _inner.GetByHashAsync(tokenHash, type);

        public Task<bool> ExistsPendingByUserAsync(
            Guid userExternalId,
            TokenType type,
            DateTimeOffset now) => _inner.ExistsPendingByUserAsync(userExternalId, type, now);

        public Task UpdateAsync(Token token) => _inner.UpdateAsync(token);

        public Task<bool> TryMarkAsUsedAsync(Token token) => _inner.TryMarkAsUsedAsync(token);

        public Task InvalidatePendingByUserAsync(
            Guid userExternalId,
            TokenType type,
            DateTimeOffset invalidatedAt) => _inner.InvalidatePendingByUserAsync(userExternalId, type, invalidatedAt);
    }
}
