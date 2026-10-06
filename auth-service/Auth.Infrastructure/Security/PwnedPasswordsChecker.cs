namespace Ouroboros.Auth.Infrastructure.Security;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Policies;

/// <summary>
/// API Pwned Passwords (Have I Been Pwned) com k-anonymity: so os 5 primeiros caracteres hexadecimais do SHA-1 saem
/// do servico, e a comparacao do restante acontece aqui.
/// </summary>
public sealed class PwnedPasswordsChecker : IBreachedPasswordChecker
{
    public static readonly Uri BaseAddress = new("https://api.pwnedpasswords.com/");
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private const int PrefixLength = 5;

    private readonly HttpClient _httpClient;
    private readonly ILogger<PwnedPasswordsChecker> _logger;

    public PwnedPasswordsChecker(
        HttpClient httpClient,
        ILogger<PwnedPasswordsChecker> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> IsBreachedAsync(string password)
    {
        // SHA-1 aqui e o protocolo do servico externo, nao armazenamento de senha.
        var sha1 = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(PasswordPolicy.Normalize(password))));
        var prefix = sha1[..PrefixLength];
        var suffix = sha1[PrefixLength..];

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"range/{prefix}");

            // Padding: o servico devolve linhas falsas (contagem 0), escondendo o tamanho real do resultado.
            request.Headers.Add("Add-Padding", "true");

            using var response = await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();

            return ContainsSuffix(body, suffix);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // Fail-open: o motivo nunca inclui a senha nem o hash.
            _logger.LogWarning("Pwned Passwords unavailable, skipping the breached password check: {Reason}", e.GetType().Name);
            return false;
        }
    }

    private static bool ContainsSuffix(
        string body,
        string suffix)
    {
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split(':');

            if (parts.Length == 2
                && parts[0].Equals(suffix, StringComparison.OrdinalIgnoreCase)
                && long.TryParse(parts[1], out var count)
                && count > 0)
            {
                return true;
            }
        }

        return false;
    }
}
