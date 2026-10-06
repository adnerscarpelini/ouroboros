namespace Ouroboros.Auth.Api.Configuration;

using Ouroboros.Auth.Application.Gateways;

/// <summary>
/// Monta o <see cref="IRequestContext"/> a partir da requisicao atual. O IP de origem ja vem resolvido pelos forwarded
/// headers (so de proxies confiaveis, ver ForwardedHeadersConfiguration).
/// </summary>
public sealed class HttpRequestContext : IRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpRequestContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
}
