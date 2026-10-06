namespace Ouroboros.Auth.Integration.Tests.Infrastructure;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Infrastructure.Persistence;
using Ouroboros.Auth.Infrastructure.Security;
using Xunit;

public sealed record LoginTokens(string AccessToken, string RefreshToken);

// Ajudante dos testes de integracao: chama a API como um cliente e prepara ou confere dados direto no banco.
public sealed class TestApi : IDisposable
{
    public const string Password = "Correct-Password-123";

    // O hash custa 600 mil iteracoes: um por senha, reaproveitado entre os usuarios de teste.
    private static readonly ConcurrentDictionary<string, string> HashCache = new();

    private readonly AuthApiFixture _fixture;
    private readonly HttpClient _client;
    private readonly Sha256TokenGenerator _tokenGenerator = new();

    public TestApi(AuthApiFixture fixture, HttpClient? client = null)
    {
        _fixture = fixture;
        _client = client ?? fixture.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    // Cada chamada sai de um IP novo, pra o limite por IP nao interferir em testes que nao tratam dele.
    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        string? bearer = null,
        string? remoteIp = null)
    {
        var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        request.Headers.Add(AuthApiFactory.RemoteIpHeader, remoteIp ?? AuthApiFixture.NextIp());

        return _client.SendAsync(request);
    }

    public Task<HttpResponseMessage> PostAsync(string path, object? body, string? bearer = null, string? remoteIp = null) =>
        SendAsync(HttpMethod.Post, path, body, bearer, remoteIp);

    public Task<HttpResponseMessage> GetAsync(string path, string? bearer = null) =>
        SendAsync(HttpMethod.Get, path, null, bearer);

    public async Task<User> CreateUserAsync(string login, string? email = null, bool confirmed = true, string password = Password)
    {
        var user = User.Create(login, "Integration Test", email ?? $"{login}@example.com", HashCache.GetOrAdd(password, value => new Pbkdf2PasswordHasher().Hash(value)));

        if (confirmed)
        {
            user.ConfirmEmail();
        }

        await using var session = _fixture.CreateSession();
        await new DapperUserRepository(session, NullLogger<DapperUserRepository>.Instance).AddAsync(user);

        return user;
    }

    public Task SetRoleAsync(Guid userExternalId, UserRole role) =>
        ExecuteAsync("UPDATE auth.users SET role = @Role WHERE external_id = @ExternalId;", new { Role = role.ToString(), ExternalId = userExternalId });

    // Grava um token de e-mail ou de reset valido e devolve o valor em claro, que na API so sai por e-mail.
    public async Task<string> AddTokenAsync(Guid userExternalId, TokenType type, TimeSpan? lifetime = null)
    {
        var raw = _tokenGenerator.Generate();
        var token = Token.Create(userExternalId, type, _tokenGenerator.Hash(raw), DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)));

        await using var session = _fixture.CreateSession();
        await new DapperTokenRepository(session).AddAsync(token);

        return raw;
    }

    public Task ExpireTokenAsync(string rawToken) =>
        ExecuteAsync(
            "UPDATE auth.tokens SET expires_at = DATEADD(MINUTE, -1, SYSDATETIMEOFFSET()) WHERE token_hash = @Hash;",
            new { Hash = _tokenGenerator.Hash(rawToken) });

    public Task ExpireRefreshTokenAsync(string rawToken) =>
        ExecuteAsync(
            "UPDATE auth.refresh_tokens SET expires_at = DATEADD(MINUTE, -1, SYSDATETIMEOFFSET()) WHERE token_hash = @Hash;",
            new { Hash = _tokenGenerator.Hash(rawToken) });

    public async Task<LoginTokens> LoginAsync(string login, string password = Password)
    {
        var response = await PostAsync("/api/auth/login", new { login, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadTokensAsync(response);
    }

    public static async Task<LoginTokens> ReadTokensAsync(HttpResponseMessage response)
    {
        using var document = await ReadJsonAsync(response);

        return new LoginTokens(
            document.RootElement.GetProperty("accessToken").GetString()!,
            document.RootElement.GetProperty("refreshToken").GetString()!);
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    public static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        using var document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("error").GetString();
    }

    public async Task<T?> QueryAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.ExecuteScalarAsync<T>(sql, parameters);
    }

    public async Task<int> ExecuteAsync(string sql, object? parameters = null)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.ExecuteAsync(sql, parameters);
    }

    public async Task<List<T>> QueryListAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return (await connection.QueryAsync<T>(sql, parameters)).ToList();
    }

    public async Task<int> CountUsersAsync() => await QueryAsync<int>("SELECT COUNT(*) FROM auth.users;");

    public async Task<int> CountTokensAsync(Guid userExternalId, TokenType type)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM auth.tokens AS tokens
            INNER JOIN auth.users AS users ON users.id = tokens.user_id
            WHERE users.external_id = @ExternalId AND tokens.type = @Type;
            """;

        return await QueryAsync<int>(sql, new { ExternalId = userExternalId, Type = type.ToString() });
    }

    public async Task<int> CountActiveRefreshTokensAsync(Guid userExternalId)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM auth.refresh_tokens AS refreshTokens
            INNER JOIN auth.users AS users ON users.id = refreshTokens.user_id
            WHERE users.external_id = @ExternalId
                AND refreshTokens.revoked_at IS NULL
                AND refreshTokens.expires_at > SYSDATETIMEOFFSET();
            """;

        return await QueryAsync<int>(sql, new { ExternalId = userExternalId });
    }

    public Task<Guid> GetExternalIdAsync(string login) =>
        QueryAsync<Guid>("SELECT external_id FROM auth.users WHERE login = @Login;", new { Login = login });
}
