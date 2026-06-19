using System.Net;
using System.Net.Sockets;
using LightingProxy.Core.Abstractions;
using LightingProxy.Core.Telemetry;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Core.Proxies;

public sealed class StcpProxyHandler : IProxyHandler
{
    public LightingProxy.Domain.Enums.ProtocolType Protocol => LightingProxy.Domain.Enums.ProtocolType.Stcp;

    public Dictionary<string, string> BuildMetadata(ProxyConfigBase config)
        => ProxyMetadataBuilder.Build(config);

    public Task StartServerAsync(ProxyDefinition definition, IServerProxyContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task HandleClientWorkConnectionAsync(ProxyDefinition definition, Stream workStream, IClientProxyContext context, CancellationToken cancellationToken = default)
    {
        await using var local = await TcpProxyHandler.OpenLocalStreamAsync(definition.Config, cancellationToken).ConfigureAwait(false);
        await TrafficRelay.BidirectionalAsync(context.Runtime, definition.Name, workStream, local, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task RunVisitorAsync(
        IPEndPoint bindEndpoint,
        string serverName,
        string secretKey,
        Func<string, string, CancellationToken, Task<Stream>> visitorConnectionFactory,
        ProxyRuntimeTracker runtime,
        CancellationToken cancellationToken)
    {
        using var listener = NetworkHelper.CreateTcpListener(bindEndpoint);

        while (!cancellationToken.IsCancellationRequested)
        {
            var socket = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
            _ = HandleVisitorClientAsync(socket, serverName, secretKey, visitorConnectionFactory, runtime, cancellationToken);
        }
    }

    private static async Task HandleVisitorClientAsync(
        Socket socket,
        string serverName,
        string secretKey,
        Func<string, string, CancellationToken, Task<Stream>> visitorConnectionFactory,
        ProxyRuntimeTracker runtime,
        CancellationToken cancellationToken)
    {
        await using var clientStream = NetworkHelper.CreateNetworkStream(socket);

        try
        {
            await using var workStream = await visitorConnectionFactory(serverName, secretKey, cancellationToken).ConfigureAwait(false);
            await TrafficRelay.BidirectionalAsync(runtime, $"visitor:{serverName}", clientStream, workStream, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Visitor connection failed.
        }
    }
}
