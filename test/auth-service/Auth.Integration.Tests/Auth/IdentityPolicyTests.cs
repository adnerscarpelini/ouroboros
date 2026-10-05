namespace Ouroboros.Auth.Integration.Tests.Auth;

using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Infrastructure.Persistence;
using Ouroboros.Auth.Infrastructure.Security;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Spec 2026092508: login e e-mail comparados sem diferenciar maiusculas, contra o banco real.
[Collection(AuthApiCollection.Name)]
public sealed class IdentityPolicyTests : IAsyncLifetime
{
    private const string Password = "Correct-Password-123";

    private readonly AuthApiFixture _fixture;
    private readonly HttpClient _client;

    public IdentityPolicyTests(AuthApiFixture fixture)
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

    [Fact]
    public async Task ShouldReturnGenericAcceptedWhenEmailExistsInAnotherCase()
    {
        await CreateActiveUserAsync("owner", "Owner@Example.com");

        var response = await RegisterAsync("someone.else", "OWNER@example.COM");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, await CountUsersAsync());
    }

    [Fact]
    public async Task ShouldRejectLoginThatExistsInAnotherCase()
    {
        await CreateActiveUserAsync("MixedCase", "mixed@example.com");

        var response = await RegisterAsync("MIXEDCASE", "other@example.com");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ShouldAuthenticateWhenLoginIsSentInAnotherCase()
    {
        await CreateActiveUserAsync("MixedCase", "mixed@example.com");

        var response = await LoginAsync("mIXEDcASE");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ShouldFindAccountByEmailInAnyCaseOnPasswordReset()
    {
        var user = await CreateActiveUserAsync("resetter", "Resetter@Example.com");

        var response = await _client.SendAsync(WithIp(
            HttpMethod.Post,
            "/api/users/password-reset/request",
            JsonContent.Create(new { loginOrEmail = "RESETTER@EXAMPLE.COM" })));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, await CountPasswordResetTokensAsync(user.ExternalId));
    }

    [Fact]
    public async Task ShouldRejectLoginOutsideAllowlistOnRegistration()
    {
        var response = await RegisterAsync("john doe", "johndoe@example.com");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountUsersAsync());
    }

    private Task<HttpResponseMessage> RegisterAsync(string login, string email)
    {
        return _client.SendAsync(WithIp(
            HttpMethod.Post,
            "/api/users",
            JsonContent.Create(new { login, fullName = "Integration Test", email, password = Password })));
    }

    private Task<HttpResponseMessage> LoginAsync(string login)
    {
        return _client.SendAsync(WithIp(
            HttpMethod.Post,
            "/api/auth/login",
            JsonContent.Create(new { login, password = Password })));
    }

    private static HttpRequestMessage WithIp(HttpMethod method, string path, HttpContent content)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add(AuthApiFactory.RemoteIpHeader, AuthApiFixture.NextIp());
        return request;
    }

    private async Task<User> CreateActiveUserAsync(string login, string email)
    {
        var user = User.Create(login, "Integration Test", email, new Pbkdf2PasswordHasher().Hash(Password));
        user.ConfirmEmail();

        await using var session = _fixture.CreateSession();
        await new DapperUserRepository(session, NullLogger<DapperUserRepository>.Instance).AddAsync(user);

        return user;
    }

    private async Task<int> CountUsersAsync()
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM auth.users;");
    }

    private async Task<int> CountPasswordResetTokensAsync(Guid userExternalId)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM auth.tokens AS tokens
            INNER JOIN auth.users AS users ON users.id = tokens.user_id
            WHERE users.external_id = @ExternalId AND tokens.type = 'PasswordReset';
            """;

        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.ExecuteScalarAsync<int>(sql, new { ExternalId = userExternalId });
    }
}
