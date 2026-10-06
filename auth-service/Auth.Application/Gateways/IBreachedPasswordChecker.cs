namespace Ouroboros.Auth.Application.Gateways;

public interface IBreachedPasswordChecker
{
    /// <summary>
    /// Indica se a senha aparece em vazamentos conhecidos. Se o servico externo estiver indisponivel, devolve
    /// <c>false</c> (fail-open): o cadastro nao fica refem de um terceiro, e a lista local continua valendo.
    /// </summary>
    Task<bool> IsBreachedAsync(string password);
}
