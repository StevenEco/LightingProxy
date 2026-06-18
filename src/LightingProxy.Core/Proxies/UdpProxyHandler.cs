using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using LightingProxy.Core.Abstractions;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Core.Proxies;

public sealed class UdpProxyHandler : IProxyHandler
{
    public LightingProxy.Domain.Enums.ProtocolType Protocol => LightingProxy.Domain.Enums.ProtocolType.Udp;

    public Dictionary<string, string> BuildMetadata(ProxyConfigBase config)
        => ProxyMetadataBuilder.Build(config);

    public async Task StartServerAsync(ProxyDefinition definition, IServerProxyContext context, CancellationToken cancellationToken = default)
    {
        var remotePort = ProxyMetadataBuilder.GetRemotePort(definition.Metadata);
        var endpoint = new IPEndPoint(IPAddress.Loopback, remotePort);
        using var socket = NetworkHelper.CreateUdpSocket(endpoint);

        while (!cancellationToken.IsCancellationRequested)
        {
            var buffer = new byte[64 * 1024];
            var result = await NetworkHelper.ReceiveUdpAsync(socket, buffer, cancellationToken).ConfigureAwait(false);
            var remote = result.RemoteEndPoint;
            var remoteKey = UdpFrameCodec.FormatEndpoint(remote);

            var frame = UdpFrameCodec.Encode(remoteKey, buffer.AsSpan(0, result.ReceivedBytes));
            await using var workStream = await context.WorkConnections.RequestWorkConnectionAsync(
                definition.Name,
                new Dictionary<string, string> { ["mode"] = "udp" },
                cancellationToken).ConfigureAwait(false);

            await workStream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);

            var responseBuffer = new byte[64 * 1024];
            var read = await workStream.ReadAsync(responseBuffer, cancellationToken).ConfigureAwait(false);
            if (UdpFrameCodec.TryDecode(responseBuffer.AsSpan(0, read), out var targetKey, out var payload) && targetKey is not null)
            {
                var target = UdpFrameCodec.ParseEndpoint(targetKey);
                await NetworkHelper.SendUdpAsync(socket, payload, target, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task HandleClientWorkConnectionAsync(ProxyDefinition definition, Stream workStream, IClientProxyContext context, CancellationToken cancellationToken = default)
    {
        using var local = NetworkHelper.CreateUdpSocket(new IPEndPoint(IPAddress.Loopback, 0));
        var requestBuffer = new byte[64 * 1024];
        var read = await workStream.ReadAsync(requestBuffer, cancellationToken).ConfigureAwait(false);

        if (!UdpFrameCodec.TryDecode(requestBuffer.AsSpan(0, read), out var remoteKey, out var payload) || remoteKey is null)
        {
            return;
        }

        await NetworkHelper.SendUdpAsync(local, payload, new IPEndPoint(IPAddress.Parse(definition.Config.LocalAddress), definition.Config.LocalPort), cancellationToken).ConfigureAwait(false);

        var response = new byte[64 * 1024];
        var result = await NetworkHelper.ReceiveUdpAsync(local, response, cancellationToken).ConfigureAwait(false);
        var frame = UdpFrameCodec.Encode(remoteKey, response.AsSpan(0, result.ReceivedBytes));
        await workStream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        await workStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
