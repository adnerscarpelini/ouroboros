namespace Ouroboros.Auth.Integration.Tests.Infrastructure;

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

// Spec 2026092511: Swagger so em Development, tokens fora do log fora de Development e API sem DDL no startup.
[Collection(AuthApiCollection.Name)]
public sealed class ProductionConfigurationTests : IAsyncLifetime
{
    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public ProductionConfigurationTests(AuthApiFixture fixture)
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
    public async Task ShouldServeSwaggerInDevelopment()
    {
        var response = await _api.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task ShouldNotServeSwaggerOutsideDevelopment(string environment)
    {
        var logs = new CapturingLoggerProvider();
        using var factory = CreateFactory(environment, logs);
        using var client = new TestApi(_fixture, factory.CreateClient());

        var json = await client.GetAsync("/swagger/v1/swagger.json");
        var ui = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.NotFound, json.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ui.StatusCode);
    }

    [Fact]
    public async Task ShouldLogEmailConfirmationAndPasswordResetTokensInDevelopment()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = CreateFactory("Development", logs);
        using var client = new TestApi(_fixture, factory.CreateClient());
        await client.CreateUserAsync("token.dev");

        var register = await client.PostAsync("/api/users", new { login = "dev.registered", fullName = "Dev", email = "dev.registered@example.com", password = "cavalo bateria grampo cedilha" });
        var reset = await client.PostAsync("/api/users/password-reset/request", new { loginOrEmail = "token.dev" });

        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, reset.StatusCode);
        Assert.Contains(logs.Messages, message => message.StartsWith("Email confirmation token generated for user"));
        Assert.Contains(logs.Messages, message => message.StartsWith("Password reset token generated for user"));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task ShouldNotLogEmailConfirmationOrPasswordResetTokensOutsideDevelopment(string environment)
    {
        var logs = new CapturingLoggerProvider();
        using var factory = CreateFactory(environment, logs);
        using var client = new TestApi(_fixture, factory.CreateClient());
        await client.CreateUserAsync("token.prod");

        var register = await client.PostAsync("/api/users", new { login = "prod.registered", fullName = "Prod", email = "prod.registered@example.com", password = "cavalo bateria grampo cedilha" });
        var reset = await client.PostAsync("/api/users/password-reset/request", new { loginOrEmail = "token.prod" });

        // O cadastro e o reset continuam respondendo 202: so o token deixa de ir pro log (e, sem e-mail, nao chega a ninguem).
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, reset.StatusCode);
        Assert.DoesNotContain(logs.Messages, message => message.Contains("token generated", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, await _api.QueryAsync<int>("SELECT COUNT(*) FROM auth.tokens WHERE type = 'EmailConfirmation';"));
        Assert.Equal(1, await _api.QueryAsync<int>("SELECT COUNT(*) FROM auth.tokens WHERE type = 'PasswordReset';"));
    }

    private WebApplicationFactory<Program> CreateFactory(
        string environment,
        CapturingLoggerProvider logs)
    {
        return _fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            // O UseSerilog do Program ignora outros providers: troca-se a ILoggerFactory, que e de onde o ILogger<T> sai.
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(new CapturingLoggerFactory(logs));
            });
        });
    }
}

public sealed class CapturingLoggerFactory : ILoggerFactory
{
    private readonly CapturingLoggerProvider _provider;

    public CapturingLoggerFactory(CapturingLoggerProvider provider)
    {
        _provider = provider;
    }

    public ILogger CreateLogger(string categoryName) => _provider.CreateLogger(categoryName);

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }
}

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _messages = new();

    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_messages)
            {
                return _messages.ToList();
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose()
    {
    }

    private void Add(string message)
    {
        lock (_messages)
        {
            _messages.Add(message);
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly CapturingLoggerProvider _provider;

        public CapturingLogger(CapturingLoggerProvider provider)
        {
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _provider.Add(formatter(state, exception));
        }
    }
}
