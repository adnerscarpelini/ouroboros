namespace Ouroboros.Auth.Application.Fakes;

using Ouroboros.Auth.Application.Gateways;

public sealed class FakeBreachedPasswordChecker : IBreachedPasswordChecker
{
    public HashSet<string> BreachedPasswords { get; } = new();

    public List<string> Checked { get; } = new();

    public Task<bool> IsBreachedAsync(string password)
    {
        Checked.Add(password);
        return Task.FromResult(BreachedPasswords.Contains(password));
    }
}
