namespace Ouroboros.Auth.Api.Configuration;

using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

public static class ForwardedHeadersConfiguration
{
    public static IServiceCollection AddTrustedForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var proxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
        var networks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];

        var parsedProxies = proxies.Select(value =>
            IPAddress.TryParse(value, out var address)
                ? address
                : throw new InvalidOperationException($"Invalid trusted proxy IP: {value}")).ToArray();

        var parsedNetworks = networks.Select(value =>
            System.Net.IPNetwork.TryParse(value, out var network)
                ? network
                : throw new InvalidOperationException($"Invalid trusted proxy network: {value}")).ToArray();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = parsedProxies.Length == 0 && parsedNetworks.Length == 0
                ? ForwardedHeaders.None
                : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var proxy in parsedProxies)
            {
                options.KnownProxies.Add(proxy);
            }

            foreach (var network in parsedNetworks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });

        return services;
    }
}
