namespace Ouroboros.Auth.Application.Gateways;

/// <summary>
/// Dados da requisicao que a auditoria registra. A Application nao conhece <c>HttpContext</c>: a Api monta este contexto.
/// </summary>
public interface IRequestContext
{
    /// <summary>Texto do IPv4 ou IPv6 de origem, ja resolvido pelos forwarded headers confiaveis. Nulo fora de uma requisicao.</summary>
    string? IpAddress { get; }

    string? UserAgent { get; }
}
