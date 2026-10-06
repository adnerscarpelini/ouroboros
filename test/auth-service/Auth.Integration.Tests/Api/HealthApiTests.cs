namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Ouroboros.Auth.Api.Health;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Serilog.Events;
using Xunit;

// Spec 2026092514: liveness e readiness, resposta so de status e fora do log de requisicoes.
[Collection(AuthApiCollection.Name)]
public sealed class HealthApiTests
{
    private readonly AuthApiFixture _fixture;

    public HealthApiTests(AuthApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task ShouldRespondHealthyWithoutAuthenticationWhenTheDatabaseIsUp(string path)
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ShouldNotApplyTheRateLimitToTheProbes()
    {
        using var client = _fixture.CreateClient();
        var statuses = new List<HttpStatusCode>();

        // Os limites dos testes sao de 3 requisicoes por IP: as sondas, que batem a cada poucos segundos, nao entram.
        for (var request = 0; request < 15; request++)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, request % 2 == 0 ? "/health/live" : "/health/ready");
            message.Headers.Add(AuthApiFactory.RemoteIpHeader, "10.9.9.9");
            statuses.Add((await client.SendAsync(message)).StatusCode);
        }

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    [Fact]
    public async Task ShouldKeepLiveHealthyAndMakeReadyUnavailableWhenTheDatabaseIsDown()
    {
        using var factory = CreateFactoryWithDatabaseAt(await ClosedPortAsync());
        using var client = factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ShouldNotLeakAnyInternalDetailInTheUnhealthyBody()
    {
        var port = await ClosedPortAsync();
        using var factory = CreateFactoryWithDatabaseAt(port);
        using var client = factory.CreateClient();

        var ready = await client.GetAsync("/health/ready");
        var body = await ready.Content.ReadAsStringAsync();

        Assert.Equal("Unhealthy", body);
        Assert.DoesNotContain("127.0.0.1", body);
        Assert.DoesNotContain(port.ToString(), body);
        Assert.DoesNotContain("Server=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("description", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{", body);
    }

    [Fact]
    public async Task ShouldGiveUpAfterTwoSecondsWhenTheDatabaseAcceptsTheConnectionButNeverAnswers()
    {
        using var silentServer = new TcpListener(IPAddress.Loopback, 0);
        silentServer.Start();
        _ = AcceptAndStayQuietAsync(silentServer);
        using var factory = CreateFactoryWithDatabaseAt(((IPEndPoint)silentServer.LocalEndpoint).Port);
        using var client = factory.CreateClient();

        var stopwatch = Stopwatch.StartNew();
        var ready = await client.GetAsync("/health/ready");
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task ShouldKeepSuccessfulProbesOutOfTheRequestLogButLogFailures()
    {
        var originalOutput = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);

        try
        {
            using var healthy = _fixture.Factory.WithWebHostBuilder(_ => { });
            using var healthyClient = healthy.CreateClient();
            await healthyClient.GetAsync("/health/live");
            await healthyClient.GetAsync("/health/ready");
            await healthyClient.PostAsync("/api/auth/logout-all", null);

            using var broken = CreateFactoryWithDatabaseAt(await ClosedPortAsync());
            using var brokenClient = broken.CreateClient();
            await brokenClient.GetAsync("/health/ready");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        var lines = captured.ToString().Split('\n');

        Assert.Contains(lines, line => line.Contains("HTTP POST /api/auth/logout-all responded 401"));
        Assert.DoesNotContain(lines, line => line.Contains("HTTP GET /health/live"));
        Assert.DoesNotContain(lines, line => line.Contains("HTTP GET /health/ready responded 200"));
        Assert.Contains(lines, line => line.Contains("HTTP GET /health/ready responded 503"));
    }

    [Theory]
    [InlineData("/health/live", 200, null, LogEventLevel.Verbose)]
    [InlineData("/health/ready", 200, null, LogEventLevel.Verbose)]
    [InlineData("/HEALTH/READY", 200, null, LogEventLevel.Verbose)]
    [InlineData("/health/ready", 503, null, LogEventLevel.Error)]
    [InlineData("/health/live", 200, typeof(InvalidOperationException), LogEventLevel.Error)]
    [InlineData("/api/auth/login", 200, null, LogEventLevel.Information)]
    [InlineData("/api/auth/login", 401, null, LogEventLevel.Information)]
    [InlineData("/api/auth/login", 500, null, LogEventLevel.Error)]
    public void ShouldChooseTheRequestLogLevelByPathAndOutcome(
        string path,
        int statusCode,
        Type? exceptionType,
        LogEventLevel expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.StatusCode = statusCode;
        var exception = exceptionType is null ? null : (Exception)Activator.CreateInstance(exceptionType)!;

        Assert.Equal(expected, HealthConfiguration.GetRequestLogLevel(context, 1, exception));
    }

    private WebApplicationFactory<Program> CreateFactoryWithDatabaseAt(int port)
    {
        var connectionString = $"Server=127.0.0.1,{port};Database=master;User Id=sa;Password=Unreachable-Password-1;Encrypt=False;Connect Timeout=5";

        return _fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Default", connectionString));
    }

    // Uma porta que acabou de ser liberada: conectar nela e recusado, como num SQL Server parado.
    private static async Task<int> ClosedPortAsync()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        await Task.Yield();

        return port;
    }

    private static async Task AcceptAndStayQuietAsync(TcpListener listener)
    {
        try
        {
            while (true)
            {
                _ = await listener.AcceptTcpClientAsync();
            }
        }
        catch (Exception e) when (e is ObjectDisposedException or SocketException or InvalidOperationException)
        {
            // O listener foi encerrado no fim do teste.
        }
    }
}
