namespace Ouroboros.Auth.Integration.Tests.Infrastructure;

using System.Collections.Concurrent;
using Ouroboros.Auth.Application.Gateways;

public sealed class FakeBreachedPasswordChecker : IBreachedPasswordChecker
{
    private readonly ConcurrentDictionary<string, byte> _breached = new();

    public void MarkAsBreached(string password) => _breached[password] = 0;

    public Task<bool> IsBreachedAsync(string password) => Task.FromResult(_breached.ContainsKey(password));
}
