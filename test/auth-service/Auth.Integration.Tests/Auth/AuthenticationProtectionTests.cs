namespace Ouroboros.Auth.Integration.Tests.Auth;

using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Infrastructure.Persistence;
using Ouroboros.Auth.Infrastructure.Security;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Spec 2026092501: limites por IP, proxy confiavel e bloqueio por conta contra o banco real.
[Collection(AuthApiCollection.Name)]
public sealed class AuthenticationProtectionTests : IAsyncLifetime
{
    private const string Password = "Correct-Password-123";

    private readonly AuthApiFixture _fixture;
    private readonly HttpClient _client;

    public AuthenticationProtectionTests(AuthApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("/api/auth/login", """{ "login": "nobody", "password": "wrong" }""")]
    [InlineData("/api/auth/refresh", """{ "refreshToken": "invalid-token" }""")]
    [InlineData("/api/users/confirm-email", """{ "token": "invalid-token" }""")]
    public async Task ShouldReturnTooManyRequestsWhenIpExceedsPolicyLimit(string path, string body)
    {
        var ip = AuthApiFixture.NextIp();

        for (var attempt = 0; attempt < AuthApiFixture.LowPermitLimit; attempt++)
        {
            var response = await PostAsync(path, body, ip);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var rejected = await PostAsync(path, body, ip);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task ShouldLimitByForwardedIpWhenRequestComesFromTrustedProxy()
    {
        var blockedClient = AuthApiFixture.NextIp();
        var otherClient = AuthApiFixture.NextIp();

        for (var attempt = 0; attempt < AuthApiFixture.LowPermitLimit; attempt++)
        {
            await LoginAsync("nobody", "wrong", AuthApiFixture.TrustedProxyIp, blockedClient);
        }

        var blocked = await LoginAsync("nobody", "wrong", AuthApiFixture.TrustedProxyIp, blockedClient);
        var other = await LoginAsync("nobody", "wrong", AuthApiFixture.TrustedProxyIp, otherClient);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
    }

    [Fact]
    public async Task ShouldIgnoreForwardedIpWhenRequestComesFromUntrustedOrigin()
    {
        var untrustedOrigin = AuthApiFixture.NextIp();

        // Cada requisicao forja um X-Forwarded-For diferente: se fosse aceito, nenhuma estouraria o limite.
        for (var attempt = 0; attempt < AuthApiFixture.LowPermitLimit; attempt++)
        {
            await LoginAsync("nobody", "wrong", untrustedOrigin, AuthApiFixture.NextIp());
        }

        var rejected = await LoginAsync("nobody", "wrong", untrustedOrigin, AuthApiFixture.NextIp());

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task ShouldCountConcurrentFailedAccessesWithoutLosingAny()
    {
        var user = await CreateActiveUserAsync("concurrent.fail");
        var failures = User.MaxFailedAccessAttempts - 1;

        await Task.WhenAll(Enumerable.Range(0, failures)
            .Select(_ => CreateRepository().RecordFailedAccessAsync(user.ExternalId, DateTimeOffset.UtcNow)));

        var stored = await CreateRepository().GetByExternalIdAsync(user.ExternalId);

        Assert.Equal(failures, stored!.AccessFailedCount);
        Assert.Null(stored.LockoutEnd);
    }

    [Fact]
    public async Task ShouldLockAccountOnceWhenConcurrentFailuresExceedLimit()
    {
        var user = await CreateActiveUserAsync("concurrent.lock");

        var results = await Task.WhenAll(Enumerable.Range(0, User.MaxFailedAccessAttempts * 4)
            .Select(_ => CreateRepository().RecordFailedAccessAsync(user.ExternalId, DateTimeOffset.UtcNow)));

        var stored = await CreateRepository().GetByExternalIdAsync(user.ExternalId);

        // Falhas durante o bloqueio nao contam: so uma chamada bloqueia e o contador volta a zero.
        Assert.Single(results, locked => locked);
        Assert.Equal(0, stored!.AccessFailedCount);
        Assert.True(stored.IsLockedOut(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ShouldRejectCorrectPasswordWhenConcurrentWrongLoginsLockAccount()
    {
        var user = await CreateActiveUserAsync("concurrent.login");

        await Task.WhenAll(Enumerable.Range(0, User.MaxFailedAccessAttempts)
            .Select(_ => LoginAsync(user.Login, "Wrong-Password-123", AuthApiFixture.NextIp())));

        var response = await LoginAsync(user.Login, Password, AuthApiFixture.NextIp());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldResetFailedAccessCountWhenLoginSucceeds()
    {
        var user = await CreateActiveUserAsync("reset.after.success");

        for (var attempt = 0; attempt < User.MaxFailedAccessAttempts - 1; attempt++)
        {
            await LoginAsync(user.Login, "Wrong-Password-123", AuthApiFixture.NextIp());
        }

        var response = await LoginAsync(user.Login, Password, AuthApiFixture.NextIp());
        var stored = await CreateRepository().GetByExternalIdAsync(user.ExternalId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, stored!.AccessFailedCount);
    }

    private DapperUserRepository CreateRepository() =>
        new(_fixture.CreateSession(), NullLogger<DapperUserRepository>.Instance);

    private async Task<User> CreateActiveUserAsync(string login)
    {
        var user = User.Create(login, "Integration Test", $"{login}@example.com", new Pbkdf2PasswordHasher().Hash(Password));
        user.ConfirmEmail();

        await CreateRepository().AddAsync(user);

        return user;
    }

    private Task<HttpResponseMessage> LoginAsync(string login, string password, string remoteIp, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { login, password }),
        };

        return SendAsync(request, remoteIp, forwardedFor);
    }

    private Task<HttpResponseMessage> PostAsync(string path, string body, string remoteIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };

        return SendAsync(request, remoteIp, null);
    }

    private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string remoteIp, string? forwardedFor)
    {
        request.Headers.Add(AuthApiFactory.RemoteIpHeader, remoteIp);

        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return _client.SendAsync(request);
    }
}
