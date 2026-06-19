using System.Net;
using System.Net.Sockets;
using System.Text;
using LightingProxy.Core.Abstractions;
using LightingProxy.Core.Telemetry;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Core.Proxies;

public sealed class TcpmuxProxyHandler : IProxyHandler
{
    public LightingProxy.Domain.Enums.ProtocolType Protocol => LightingProxy.Domain.Enums.ProtocolType.Tcpmux;

    public Dictionary<string, string> BuildMetadata(ProxyConfigBase config)
        => ProxyMetadataBuilder.Build(config);

    public Task StartServerAsync(ProxyDefinition definition, IServerProxyContext context, CancellationToken cancellationToken = default)
    {
        foreach (var host in HttpProxyHandler.GetHosts(definition))
        {
            context.RegisterTcpmuxRoute(host, definition.Name);
        }

        return Task.CompletedTask;
    }

    public async Task HandleClientWorkConnectionAsync(ProxyDefinition definition, Stream workStream, IClientProxyContext context, CancellationToken cancellationToken = default)
    {
        await using var local = await TcpProxyHandler.OpenLocalStreamAsync(definition.Config, cancellationToken).ConfigureAwait(false);
        await TrafficRelay.BidirectionalAsync(context.Runtime, definition.Name, workStream, local, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task RunTcpmuxListenerAsync(
        int port,
        Func<string, CancellationToken, Task<Stream>> workConnectionFactory,
        CancellationToken cancellationToken)
    {
        using var listener = NetworkHelper.CreateTcpListener(new IPEndPoint(IPAddress.Loopback, port));

        while (!cancellationToken.IsCancellationRequested)
        {
            var socket = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
            _ = HandleTcpmuxClientAsync(socket, workConnectionFactory, cancellationToken);
        }
    }

    private static async Task HandleTcpmuxClientAsync(Socket socket, Func<string, CancellationToken, Task<Stream>> workConnectionFactory, CancellationToken cancellationToken)
    {
        await using var stream = NetworkHelper.CreateNetworkStream(socket);
        var (parsedHost, headers, tail) = await HttpStreamHelper.ReadHttpPartsAsync(stream, cancellationToken).ConfigureAwait(false);
        var host = parsedHost ?? HttpStreamHelper.ParseConnectHost(headers);

        if (string.IsNullOrWhiteSpace(host))
        {
            await stream.WriteAsync("HTTP/1.1 400 Bad Request\r\n\r\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var workStream = await workConnectionFactory(host, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync("HTTP/1.1 200 Connection Established\r\n\r\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);

        if (tail.Length > 0)
        {
            await workStream.WriteAsync(tail, cancellationToken).ConfigureAwait(false);
            await workStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        await StreamRelay.RelayBidirectionalAsync(stream, workStream, cancellationToken).ConfigureAwait(false);
    }
}
