namespace Ouroboros.Auth.Application.Gateways;

public interface IPasswordHasher
{
    string DummyHash { get; }

    string Hash(string password);

    bool Verify(string password, string passwordHash);

    /// <summary>
    /// Indica se o hash foi gerado com um custo menor que o atual e deve ser refeito no proximo login bem-sucedido.
    /// </summary>
    bool NeedsRehash(string passwordHash);
}
