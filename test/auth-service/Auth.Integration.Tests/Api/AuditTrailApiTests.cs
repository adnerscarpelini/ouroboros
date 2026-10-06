namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Net;
using Microsoft.Data.SqlClient;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Infrastructure.Persistence;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Spec 2026092519: trilha de auditoria dos eventos de seguranca, pela API contra o banco real.
[Collection(AuthApiCollection.Name)]
public sealed class AuditTrailApiTests : IAsyncLifetime
{
    private const string NewPassword = "Brand-New-Password-5678";

    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public AuditTrailApiTests(AuthApiFixture fixture)
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

    private sealed class AuditRow
    {
        public string EventType { get; init; } = null!;

        public string Outcome { get; init; } = null!;

        public Guid? UserExternalId { get; init; }

        public Guid? ActorExternalId { get; init; }

        public Guid? SessionId { get; init; }

        public string? IpAddress { get; init; }

        public string? UserAgent { get; init; }

        public string? Reason { get; init; }
    }

    [Fact]
    public async Task ShouldRecordUserRegisteredWhenTheRegistrationSucceeds()
    {
        var response = await _api.PostAsync("/api/users", new { login = "audit.register", fullName = "Audit", email = "audit.register@example.com", password = "cavalo bateria grampo cedilha" });

        var row = Assert.Single(await EventsAsync());
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("UserRegistered", row.EventType);
        Assert.Equal("Success", row.Outcome);
        Assert.Equal(await _api.GetExternalIdAsync("audit.register"), row.UserExternalId);
    }

    [Fact]
    public async Task ShouldRecordNothingWhenTheRegistrationIsRejectedOrTheEmailIsTaken()
    {
        await _api.CreateUserAsync("audit.taken", "audit.taken@example.com");
        await _api.PostAsync("/api/users", new { login = "audit.weak", fullName = "Audit", email = "audit.weak@example.com", password = "short" });
        await _api.PostAsync("/api/users", new { login = "audit.other", fullName = "Audit", email = "audit.taken@example.com", password = "cavalo bateria grampo cedilha" });

        Assert.Empty(await EventsAsync());
    }

    [Fact]
    public async Task ShouldRecordEmailConfirmedWhenTheTokenIsValid()
    {
        var user = await _api.CreateUserAsync("audit.confirm", confirmed: false);
        var token = await _api.AddTokenAsync(user.ExternalId, TokenType.EmailConfirmation);

        var response = await _api.PostAsync("/api/users/confirm-email", new { token });

        var row = Assert.Single(await EventsAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("EmailConfirmed", row.EventType);
        Assert.Equal(user.ExternalId, row.UserExternalId);
    }

    [Fact]
    public async Task ShouldRecordLoginSucceededWithTheSessionTheIpAndTheUserAgent()
    {
        var user = await _api.CreateUserAsync("audit.login");

        var response = await _api.PostAsync("/api/auth/login", new { login = "audit.login", password = TestApi.Password }, remoteIp: "10.7.7.7", userAgent: "AuditTestAgent/1.0");

        var row = Assert.Single(await EventsAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("LoginSucceeded", row.EventType);
        Assert.Equal("Success", row.Outcome);
        Assert.Equal(user.ExternalId, row.UserExternalId);
        Assert.Equal("10.7.7.7", row.IpAddress);
        Assert.Equal("AuditTestAgent/1.0", row.UserAgent);
        Assert.Equal(await _api.QueryAsync<Guid>("SELECT session_id FROM auth.refresh_tokens;"), row.SessionId);
    }

    [Fact]
    public async Task ShouldTruncateTheUserAgentAt256Characters()
    {
        await _api.CreateUserAsync("audit.agent");

        await _api.PostAsync("/api/auth/login", new { login = "audit.agent", password = TestApi.Password }, userAgent: new string('a', 300));

        Assert.Equal(256, Assert.Single(await EventsAsync()).UserAgent!.Length);
    }

    [Fact]
    public async Task ShouldRecordLoginFailedWithTheInvalidPasswordReason()
    {
        var user = await _api.CreateUserAsync("audit.wrong");

        await _api.PostAsync("/api/auth/login", new { login = "audit.wrong", password = "Wrong-Password-123" });

        var row = Assert.Single(await EventsAsync());
        Assert.Equal("LoginFailed", row.EventType);
        Assert.Equal("Failure", row.Outcome);
        Assert.Equal(user.ExternalId, row.UserExternalId);
        Assert.Equal("invalid_password", row.Reason);
    }

    [Fact]
    public async Task ShouldNeverStoreTheTypedLoginNorThePasswordWhenTheAccountDoesNotExist()
    {
        // Usuarios digitam a senha no campo de login por engano.
        await _api.PostAsync("/api/auth/login", new { login = "typed-my-password-here-by-mistake", password = "Wrong-Password-123" });

        var row = Assert.Single(await EventsAsync());
        Assert.Equal("LoginFailed", row.EventType);
        Assert.Null(row.UserExternalId);
        Assert.Equal("unknown_login", row.Reason);

        var everything = await AllAuditText();
        Assert.DoesNotContain("typed-my-password-here-by-mistake", everything);
        Assert.DoesNotContain("Wrong-Password-123", everything);
    }

    [Fact]
    public async Task ShouldRecordTheFailuresTheLockoutAndTheLockedAttempt()
    {
        var user = await _api.CreateUserAsync("audit.lock");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await _api.PostAsync("/api/auth/login", new { login = "audit.lock", password = "Wrong-Password-123" });
        }

        await _api.PostAsync("/api/auth/login", new { login = "audit.lock", password = TestApi.Password });

        var rows = await EventsAsync();
        Assert.Equal(6, rows.Count(row => row.EventType == "LoginFailed"));
        Assert.Equal(5, rows.Count(row => row is { EventType: "LoginFailed", Reason: "invalid_password" }));
        Assert.Single(rows, row => row is { EventType: "LoginFailed", Reason: "locked_out" });
        var locked = Assert.Single(rows, row => row.EventType == "AccountLockedOut");
        Assert.Equal("Failure", locked.Outcome);
        Assert.Equal(user.ExternalId, locked.UserExternalId);
        Assert.Equal("too_many_failed_attempts", locked.Reason);
    }

    [Fact]
    public async Task ShouldRecordRefreshTokenReuseWithTheSessionAndNotAnOrdinaryRefresh()
    {
        var user = await _api.CreateUserAsync("audit.reuse");
        var login = await _api.LoginAsync("audit.reuse");
        await _api.PostAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });
        await _api.PostAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });

        var rows = await EventsAsync();

        Assert.Equal(["LoginSucceeded", "RefreshTokenReuseDetected"], rows.Select(row => row.EventType));
        var reuse = rows[1];
        Assert.Equal("Failure", reuse.Outcome);
        Assert.Equal(user.ExternalId, reuse.UserExternalId);
        Assert.Equal(rows[0].SessionId, reuse.SessionId);
        Assert.Equal("reuse_detected", reuse.Reason);
    }

    [Fact]
    public async Task ShouldRecordLogoutAndLogoutAllButNotAnIdempotentLogout()
    {
        var user = await _api.CreateUserAsync("audit.logout");
        var first = await _api.LoginAsync("audit.logout");
        var second = await _api.LoginAsync("audit.logout");

        await _api.PostAsync("/api/auth/logout", new { refreshToken = first.RefreshToken });
        await _api.PostAsync("/api/auth/logout", new { refreshToken = first.RefreshToken });
        await _api.PostAsync("/api/auth/logout-all", null, second.AccessToken);

        var rows = await EventsAsync();
        Assert.Equal(["LoginSucceeded", "LoginSucceeded", "Logout", "LogoutAll"], rows.Select(row => row.EventType));
        Assert.Equal(rows[0].SessionId, rows[2].SessionId);
        Assert.Equal(user.ExternalId, rows[3].UserExternalId);
        Assert.Null(rows[3].SessionId);
    }

    [Fact]
    public async Task ShouldRecordPasswordResetRequestedOnlyWhenTheAccountExistsAndThenCompleted()
    {
        var user = await _api.CreateUserAsync("audit.reset");

        await _api.PostAsync("/api/users/password-reset/request", new { loginOrEmail = "does.not.exist" });
        await _api.PostAsync("/api/users/password-reset/request", new { loginOrEmail = "audit.reset" });
        var token = await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);
        var confirm = await _api.PostAsync("/api/users/password-reset/confirm", new { token, newPassword = NewPassword });

        var rows = await EventsAsync();
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        Assert.Equal(["PasswordResetRequested", "PasswordResetCompleted"], rows.Select(row => row.EventType));
        Assert.All(rows, row => Assert.Equal(user.ExternalId, row.UserExternalId));

        var everything = await AllAuditText();
        Assert.DoesNotContain(token, everything);
        Assert.DoesNotContain(NewPassword, everything);
    }

    [Fact]
    public async Task ShouldRecordPasswordChangedWithTheCurrentSession()
    {
        var user = await _api.CreateUserAsync("audit.change");
        var session = await _api.LoginAsync("audit.change");

        var response = await _api.SendAsync(HttpMethod.Put, "/api/users/me/password", new { currentPassword = TestApi.Password, newPassword = NewPassword }, session.AccessToken);

        var rows = await EventsAsync();
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["LoginSucceeded", "PasswordChanged"], rows.Select(row => row.EventType));
        Assert.Equal(user.ExternalId, rows[1].UserExternalId);
        Assert.Equal(rows[0].SessionId, rows[1].SessionId);
        Assert.DoesNotContain(NewPassword, await AllAuditText());
    }

    [Fact]
    public async Task ShouldRecordUserDeletedWithTheAdminAsActor()
    {
        var admin = await _api.CreateUserAsync("audit.admin");
        await _api.SetRoleAsync(admin.ExternalId, UserRole.Admin);
        var victim = await _api.CreateUserAsync("audit.victim");
        var session = await _api.LoginAsync("audit.admin");

        var response = await _api.SendAsync(HttpMethod.Delete, $"/api/users/{victim.ExternalId}", new { password = TestApi.Password }, session.AccessToken);

        var deleted = Assert.Single(await EventsAsync(), row => row.EventType == "UserDeleted");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(victim.ExternalId, deleted.UserExternalId);
        Assert.Equal(admin.ExternalId, deleted.ActorExternalId);
    }

    [Fact]
    public async Task ShouldRecordUserDeletedWithoutActorOnSelfDeletion()
    {
        var user = await _api.CreateUserAsync("audit.self");
        var session = await _api.LoginAsync("audit.self");

        await _api.SendAsync(HttpMethod.Delete, $"/api/users/{user.ExternalId}", new { password = TestApi.Password }, session.AccessToken);

        var deleted = Assert.Single(await EventsAsync(), row => row.EventType == "UserDeleted");
        Assert.Equal(user.ExternalId, deleted.UserExternalId);
        Assert.Null(deleted.ActorExternalId);
    }

    [Fact]
    public async Task ShouldKeepTheAuditEventsOfADeletedAccountBecauseThereIsNoForeignKey()
    {
        var user = await _api.CreateUserAsync("audit.survive");
        await _api.LoginAsync("audit.survive");

        await _api.ExecuteAsync("DELETE FROM auth.refresh_tokens; DELETE FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId });

        Assert.Equal(user.ExternalId, Assert.Single(await EventsAsync()).UserExternalId);
    }

    [Fact]
    public async Task ShouldRemoveTheSuccessEventWhenTheOperationIsRolledBack()
    {
        var user = await _api.CreateUserAsync("audit.rollback");
        var session = await _api.LoginAsync("audit.rollback");
        await _fixture.ResetDatabaseAsync();
        user = await _api.CreateUserAsync("audit.rollback");
        session = await _api.LoginAsync("audit.rollback");
        var before = (await EventsAsync()).Count;

        using var factory = _fixture.CreateFactoryFailingOn<IRefreshTokenRepository>(nameof(IRefreshTokenRepository.RevokeAllActiveByUserExceptSessionAsync), FaultTiming.After);
        using var failing = new TestApi(_fixture, factory.CreateClient());

        var response = await failing.SendAsync(HttpMethod.Put, "/api/users/me/password", new { currentPassword = TestApi.Password, newPassword = NewPassword }, session.AccessToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(before, (await EventsAsync()).Count);
        Assert.DoesNotContain(await EventsAsync(), row => row.EventType == "PasswordChanged");
        Assert.Equal(user.ExternalId, (await EventsAsync()).Single().UserExternalId);
    }

    [Fact]
    public async Task ShouldRemoveTheLoginSucceededEventWhenTheLoginIsRolledBack()
    {
        await _api.CreateUserAsync("audit.login.fault");

        using var factory = _fixture.CreateFactoryFailingOn<IRefreshTokenRepository>(nameof(IRefreshTokenRepository.AddAsync), FaultTiming.After);
        using var failing = new TestApi(_fixture, factory.CreateClient());

        var response = await failing.PostAsync("/api/auth/login", new { login = "audit.login.fault", password = TestApi.Password });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(await EventsAsync());
    }

    [Fact]
    public async Task ShouldKeepTheFailureEventWhenTheOperationAroundItIsRolledBack()
    {
        // Evento de falha em autocommit, depois evento de sucesso numa transacao que e desfeita.
        await using var session = _fixture.CreateSession();
        var auditLog = new DapperAuditLog(session, new FixedRequestContext());
        var user = await _api.CreateUserAsync("audit.survives");

        await auditLog.RecordAsync(new AuditEvent(AuditEventType.LoginFailed, AuditOutcome.Failure, user.ExternalId, Reason: AuditReason.InvalidPassword));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new SqlUnitOfWork(session).ExecuteAsync(async () =>
        {
            await auditLog.RecordAsync(new AuditEvent(AuditEventType.LoginSucceeded, AuditOutcome.Success, user.ExternalId));

            throw new InvalidOperationException("the operation failed");
        }));

        var row = Assert.Single(await EventsAsync());
        Assert.Equal("LoginFailed", row.EventType);
    }

    [Fact]
    public async Task ShouldNeverStoreAPasswordATokenOrAHashInAnyColumn()
    {
        var user = await _api.CreateUserAsync("audit.secrets");
        var login = await _api.LoginAsync("audit.secrets");
        var passwordHash = await _api.QueryAsync<string>("SELECT password_hash FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId });
        await _api.PostAsync("/api/auth/login", new { login = "audit.secrets", password = "Wrong-Password-123" });
        await _api.PostAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });
        await _api.PostAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });

        var everything = await AllAuditText();

        Assert.NotEmpty(everything);
        Assert.DoesNotContain(TestApi.Password, everything);
        Assert.DoesNotContain("Wrong-Password-123", everything);
        Assert.DoesNotContain(login.RefreshToken, everything);
        Assert.DoesNotContain(login.AccessToken, everything);
        Assert.DoesNotContain(passwordHash, everything);
    }

    [Theory]
    [InlineData("UnknownEvent", "Success")]
    [InlineData("LoginSucceeded", "Maybe")]
    public async Task ShouldRejectAnUnknownEventTypeOrOutcomeAtTheDatabase(
        string eventType,
        string outcome)
    {
        var error = await Assert.ThrowsAsync<SqlException>(() => _api.ExecuteAsync(
            "INSERT INTO auth.audit_events (external_id, occurred_at, event_type, outcome) VALUES (NEWID(), SYSDATETIMEOFFSET(), @EventType, @Outcome);",
            new { EventType = eventType, Outcome = outcome }));

        Assert.Equal(547, error.Number);
    }

    [Fact]
    public async Task ShouldIndexTheTableByUserAndTimeAndByTime()
    {
        var indexes = await _api.QueryListAsync<string>("SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('auth.audit_events') AND name IS NOT NULL ORDER BY name;");

        Assert.Contains("audit_events_user_external_id_occurred_at_idx", indexes);
        Assert.Contains("audit_events_occurred_at_idx", indexes);
        Assert.Contains("audit_events_external_id_key", indexes);
    }

    private async Task<List<AuditRow>> EventsAsync() =>
        await _api.QueryListAsync<AuditRow>(
            """
            SELECT
                event_type AS EventType,
                outcome AS Outcome,
                user_external_id AS UserExternalId,
                actor_external_id AS ActorExternalId,
                session_id AS SessionId,
                ip_address AS IpAddress,
                user_agent AS UserAgent,
                reason AS Reason
            FROM auth.audit_events
            ORDER BY id;
            """);

    // Todas as colunas de texto e de id de todas as linhas, numa string so, pra conferir o que nunca pode aparecer.
    private async Task<string> AllAuditText()
    {
        var rows = await _api.QueryListAsync<string>(
            """
            SELECT CONCAT_WS(N'|', event_type, outcome, CONVERT(nvarchar(50), user_external_id), CONVERT(nvarchar(50), actor_external_id),
                CONVERT(nvarchar(50), session_id), ip_address, user_agent, reason)
            FROM auth.audit_events;
            """);

        return string.Join('\n', rows);
    }

    private sealed class FixedRequestContext : IRequestContext
    {
        public string? IpAddress => "10.0.0.5";

        public string? UserAgent => "fixed-agent";
    }
}
