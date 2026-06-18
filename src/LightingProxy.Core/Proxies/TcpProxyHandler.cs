using System.Net;
using System.Net.Sockets;
using LightingProxy.Core.Abstractions;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Core.Proxies;

public sealed class TcpProxyHandler : IProxyHandler
{
    public LightingProxy.Domain.Enums.ProtocolType Protocol => LightingProxy.Domain.Enums.ProtocolType.Tcp;

    public Dictionary<string, string> BuildMetadata(ProxyConfigBase config)
        => ProxyMetadataBuilder.Build(config);

    public async Task StartServerAsync(ProxyDefinition definition, IServerProxyContext context, CancellationToken cancellationToken = default)
    {
        var remotePort = ProxyMetadataBuilder.GetRemotePort(definition.Metadata);
        var bindAddress = definition.Metadata.TryGetValue("remoteAddress", out var address) ? address : context.BindAddress;
        var endpoint = new IPEndPoint(IPAddress.Parse(bindAddress == "0.0.0.0" ? "127.0.0.1" : bindAddress), remotePort);
        using var listener = NetworkHelper.CreateTcpListener(endpoint);

        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
            _ = HandleClientAsync(definition.Name, client, context, cancellationToken);
        }
    }

    public async Task HandleClientWorkConnectionAsync(ProxyDefinition definition, Stream workStream, IClientProxyContext context, CancellationToken cancellationToken = default)
    {
        await using var local = await OpenLocalStreamAsync(definition.Config, cancellationToken).ConfigureAwait(false);
        await StreamRelay.RelayBidirectionalAsync(workStream, local, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<Stream> OpenLocalStreamAsync(ProxyConfigBase config, CancellationToken cancellationToken)
    {
        var socket = await NetworkHelper.ConnectTcpAsync(config.LocalAddress, config.LocalPort, cancellationToken).ConfigureAwait(false);
        return NetworkHelper.CreateNetworkStream(socket);
    }

    private static async Task HandleClientAsync(string proxyName, Socket clientSocket, IServerProxyContext context, CancellationToken cancellationToken)
    {
        await using var clientStream = NetworkHelper.CreateNetworkStream(clientSocket);

        try
        {
            await using var workStream = await context.WorkConnections.RequestWorkConnectionAsync(proxyName, cancellationToken: cancellationToken).ConfigureAwait(false);
            await StreamRelay.RelayBidirectionalAsync(clientStream, workStream, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Connection failed; client stream disposed by await using.
        }
    }
}
