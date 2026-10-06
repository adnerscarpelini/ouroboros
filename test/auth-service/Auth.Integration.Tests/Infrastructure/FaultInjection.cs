namespace Ouroboros.Auth.Integration.Tests.Infrastructure;

using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public enum FaultTiming
{
    // Falha antes de chamar o repositorio real: o comando nao chega ao banco.
    Before,

    // Falha depois de o comando rodar no banco: prova que o rollback desfaz uma escrita ja feita.
    After,
}

public sealed class InjectedFaultException : Exception
{
    public InjectedFaultException(string message)
        : base(message)
    {
    }
}

// Decorator de repositorio que lanca uma excecao na N-esima chamada de um metodo. A contagem vale por instancia (um por request).
public class FaultInjectingProxy<TGateway> : DispatchProxy
    where TGateway : class
{
    private TGateway _inner = null!;
    private string _method = null!;
    private FaultTiming _timing;
    private int _onCall;
    private int _calls;

    public static TGateway Create(TGateway inner, string method, FaultTiming timing, int onCall)
    {
        var proxy = Create<TGateway, FaultInjectingProxy<TGateway>>();
        var typed = (FaultInjectingProxy<TGateway>)(object)proxy;

        typed._inner = inner;
        typed._method = method;
        typed._timing = timing;
        typed._onCall = onCall;

        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        var shouldFail = method.Name == _method && Interlocked.Increment(ref _calls) == _onCall;

        if (shouldFail && _timing == FaultTiming.Before)
        {
            throw CreateFault(method);
        }

        object? result;

        try
        {
            result = method.Invoke(_inner, args);
        }
        catch (TargetInvocationException exception)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException!).Throw();
            throw;
        }

        return shouldFail ? ThrowAfter(method, result!) : result;
    }

    private static InjectedFaultException CreateFault(MethodInfo method) =>
        new($"Injected failure in {typeof(TGateway).Name}.{method.Name}.");

    private static object ThrowAfter(MethodInfo method, object result)
    {
        if (method.ReturnType == typeof(Task))
        {
            return ThrowAfterTaskAsync(method, (Task)result);
        }

        var valueType = method.ReturnType.GetGenericArguments()[0];

        return typeof(FaultInjectingProxy<TGateway>)
            .GetMethod(nameof(ThrowAfterTaskOfAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(valueType)
            .Invoke(null, [method, result])!;
    }

    private static async Task ThrowAfterTaskAsync(MethodInfo method, Task task)
    {
        await task;
        throw CreateFault(method);
    }

    private static async Task<TResult> ThrowAfterTaskOfAsync<TResult>(MethodInfo method, Task<TResult> task)
    {
        await task;
        throw CreateFault(method);
    }
}

public static class FaultInjectionExtensions
{
    // Troca o gateway registrado por um decorator que falha; o repositorio real continua sendo construido pelo DI.
    public static IServiceCollection FailOn<TGateway>(
        this IServiceCollection services,
        string method,
        FaultTiming timing = FaultTiming.Before,
        int onCall = 1)
        where TGateway : class
    {
        var descriptor = services.Single(service => service.ServiceType == typeof(TGateway));
        var implementationType = descriptor.ImplementationType
            ?? throw new InvalidOperationException($"{typeof(TGateway).Name} must be registered by implementation type.");

        services.RemoveAll<TGateway>();
        services.AddScoped(provider =>
        {
            var inner = (TGateway)ActivatorUtilities.CreateInstance(provider, implementationType);

            return FaultInjectingProxy<TGateway>.Create(inner, method, timing, onCall);
        });

        return services;
    }
}
