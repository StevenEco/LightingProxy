using System.Net;
using System.Net.Sockets;
using LightingProxy.Core.Abstractions;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Core.Proxies;

public sealed class HttpsProxyHandler : IProxyHandler
{
    public LightingProxy.Domain.Enums.ProtocolType Protocol => LightingProxy.Domain.Enums.ProtocolType.Https;

    public Dictionary<string, string> BuildMetadata(ProxyConfigBase config)
        => ProxyMetadataBuilder.Build(config);

    public Task StartServerAsync(ProxyDefinition definition, IServerProxyContext context, CancellationToken cancellationToken = default)
    {
        foreach (var host in HttpProxyHandler.GetHosts(definition))
        {
            context.RegisterHttpsRoute(host, definition.Name);
        }

        return Task.CompletedTask;
    }

    public async Task HandleClientWorkConnectionAsync(ProxyDefinition definition, Stream workStream, IClientProxyContext context, CancellationToken cancellationToken = default)
    {
        await using var local = await TcpProxyHandler.OpenLocalStreamAsync(definition.Config, cancellationToken).ConfigureAwait(false);
        await StreamRelay.RelayBidirectionalAsync(workStream, local, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task RunHttpsListenerAsync(
        int port,
        Func<string, CancellationToken, Task<Stream>> workConnectionFactory,
        CancellationToken cancellationToken)
    {
        using var listener = NetworkHelper.CreateTcpListener(new IPEndPoint(IPAddress.Loopback, port));

        while (!cancellationToken.IsCancellationRequested)
        {
            var socket = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
            _ = HandleHttpsClientAsync(socket, workConnectionFactory, cancellationToken);
        }
    }

    private static async Task HandleHttpsClientAsync(Socket socket, Func<string, CancellationToken, Task<Stream>> workConnectionFactory, CancellationToken cancellationToken)
    {
        await using var stream = NetworkHelper.CreateNetworkStream(socket);
        var buffer = new byte[16 * 1024];
        var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (!SniParser.TryGetServerName(buffer.AsSpan(0, read), out var host) || string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        await using var workStream = await workConnectionFactory(host, cancellationToken).ConfigureAwait(false);
        await workStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        await StreamRelay.RelayBidirectionalAsync(stream, workStream, cancellationToken).ConfigureAwait(false);
    }
}
