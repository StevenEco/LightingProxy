using System.Net;
using System.Net.Sockets;
using LightingProxy.Core.Abstractions;
using LightingProxy.Core.Telemetry;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Core.Proxies;

public sealed class HttpProxyHandler : IProxyHandler
{
    public LightingProxy.Domain.Enums.ProtocolType Protocol => LightingProxy.Domain.Enums.ProtocolType.Http;

    public Dictionary<string, string> BuildMetadata(ProxyConfigBase config)
        => ProxyMetadataBuilder.Build(config);

    public Task StartServerAsync(ProxyDefinition definition, IServerProxyContext context, CancellationToken cancellationToken = default)
    {
        foreach (var host in GetHosts(definition))
        {
            context.RegisterHttpRoute(host, definition.Name);
        }

        return Task.CompletedTask;
    }

    public async Task HandleClientWorkConnectionAsync(ProxyDefinition definition, Stream workStream, IClientProxyContext context, CancellationToken cancellationToken = default)
    {
        await using var local = await TcpProxyHandler.OpenLocalStreamAsync(definition.Config, cancellationToken).ConfigureAwait(false);
        await TrafficRelay.BidirectionalAsync(context.Runtime, definition.Name, workStream, local, cancellationToken).ConfigureAwait(false);
    }

    internal static IEnumerable<string> GetHosts(ProxyDefinition definition)
    {
        if (definition.Metadata.TryGetValue("customDomains", out var domains))
        {
            foreach (var domain in domains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return domain;
            }
        }

        if (definition.Metadata.TryGetValue("subDomain", out var subDomain) && !string.IsNullOrWhiteSpace(subDomain))
        {
            yield return subDomain;
        }
    }

    internal static async Task RunHttpListenerAsync(
        int port,
        Func<string, CancellationToken, Task<Stream>> workConnectionFactory,
        CancellationToken cancellationToken)
    {
        using var listener = NetworkHelper.CreateTcpListener(new IPEndPoint(IPAddress.Loopback, port));

        while (!cancellationToken.IsCancellationRequested)
        {
            var socket = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
            _ = HandleHttpClientAsync(socket, workConnectionFactory, cancellationToken);
        }
    }

    private static async Task HandleHttpClientAsync(Socket socket, Func<string, CancellationToken, Task<Stream>> workConnectionFactory, CancellationToken cancellationToken)
    {
        await using var stream = NetworkHelper.CreateNetworkStream(socket);
        var (host, prefix) = await HttpStreamHelper.ReadHttpPrefixAsync(stream, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        await using var workStream = await workConnectionFactory(host, cancellationToken).ConfigureAwait(false);
        await workStream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await StreamRelay.RelayBidirectionalAsync(stream, workStream, cancellationToken).ConfigureAwait(false);
    }
}
