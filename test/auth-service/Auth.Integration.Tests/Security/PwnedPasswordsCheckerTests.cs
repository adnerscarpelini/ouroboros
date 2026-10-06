namespace Ouroboros.Auth.Integration.Tests.Security;

using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Ouroboros.Auth.Infrastructure.Security;
using Xunit;

// Spec 2026092509: k-anonymity, padding e fail-open do Pwned Passwords, com o servico simulado.
public sealed class PwnedPasswordsCheckerTests
{
    private const string Password = "Str0ng-Passphrase-1";

    private static readonly string Sha1 = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(Password)));

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_respond(request));
        }
    }

    private sealed class CapturingLogger : ILogger<PwnedPasswordsChecker>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

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
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    private static PwnedPasswordsChecker CreateChecker(
        StubHandler handler,
        CapturingLogger? logger = null)
    {
        var client = new HttpClient(handler) { BaseAddress = PwnedPasswordsChecker.BaseAddress };

        return new PwnedPasswordsChecker(client, logger ?? new CapturingLogger());
    }

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Fact]
    public async Task ShouldSendOnlyTheFirstFiveHexCharactersAndAskForPadding()
    {
        var handler = new StubHandler(_ => Ok("0000000000000000000000000000000000A:0"));
        var checker = CreateChecker(handler);

        await checker.IsBreachedAsync(Password);

        var request = Assert.Single(handler.Requests);
        Assert.Equal($"/range/{Sha1[..5]}", request.RequestUri!.AbsolutePath);
        Assert.DoesNotContain(Sha1[5..], request.RequestUri.ToString());
        Assert.Equal("true", Assert.Single(request.Headers.GetValues("Add-Padding")));
    }

    [Fact]
    public async Task ShouldReportBreachedWhenSuffixIsInTheResponseWithPositiveCount()
    {
        var handler = new StubHandler(_ => Ok($"0000000000000000000000000000000000A:0\r\n{Sha1[5..]}:42\r\n"));

        Assert.True(await CreateChecker(handler).IsBreachedAsync(Password));
    }

    [Fact]
    public async Task ShouldIgnorePaddingEntriesWithCountZero()
    {
        var handler = new StubHandler(_ => Ok($"{Sha1[5..]}:0\r\n"));

        Assert.False(await CreateChecker(handler).IsBreachedAsync(Password));
    }

    [Fact]
    public async Task ShouldNotReportBreachedWhenSuffixIsAbsent()
    {
        var handler = new StubHandler(_ => Ok("0000000000000000000000000000000000A:7\r\n"));

        Assert.False(await CreateChecker(handler).IsBreachedAsync(Password));
    }

    [Fact]
    public async Task ShouldFailOpenAndLogWarningWhenServiceReturnsError()
    {
        var logger = new CapturingLogger();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var breached = await CreateChecker(handler, logger).IsBreachedAsync(Password);

        Assert.False(breached);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.DoesNotContain(Password, entry.Message);
        Assert.DoesNotContain(Sha1, entry.Message);
    }

    [Fact]
    public async Task ShouldFailOpenAndLogWarningWhenRequestTimesOut()
    {
        var logger = new CapturingLogger();
        var handler = new StubHandler(_ => throw new TaskCanceledException());

        var breached = await CreateChecker(handler, logger).IsBreachedAsync(Password);

        Assert.False(breached);
        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public void ShouldUseTwoSecondTimeout()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), PwnedPasswordsChecker.Timeout);
    }
}
