using System.Net;
using LightingProxy.Core.Abstractions;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Core.Proxies;

public sealed class KcpProxyHandler : IProxyHandler
{
    public LightingProxy.Domain.Enums.ProtocolType Protocol => LightingProxy.Domain.Enums.ProtocolType.Kcp;

    public Dictionary<string, string> BuildMetadata(ProxyConfigBase config)
    {
        var metadata = ProxyMetadataBuilder.Build(config);
        metadata["transport"] = "kcp";
        return metadata;
    }

    public async Task StartServerAsync(ProxyDefinition definition, IServerProxyContext context, CancellationToken cancellationToken = default)
    {
        var remotePort = ProxyMetadataBuilder.GetRemotePort(definition.Metadata);
        using var channel = new KcpChannel(new IPEndPoint(IPAddress.Loopback, remotePort));

        while (!cancellationToken.IsCancellationRequested)
        {
            var packet = await channel.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (packet is null)
            {
                continue;
            }

            await using var workStream = await context.WorkConnections.RequestWorkConnectionAsync(
                definition.Name,
                new Dictionary<string, string> { ["mode"] = "kcp" },
                cancellationToken).ConfigureAwait(false);

            await workStream.WriteAsync(packet.Value.Payload, cancellationToken).ConfigureAwait(false);

            var responseBuffer = new byte[64 * 1024];
            var read = await workStream.ReadAsync(responseBuffer, cancellationToken).ConfigureAwait(false);
            await channel.SendAsync(packet.Value.Remote, responseBuffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task HandleClientWorkConnectionAsync(ProxyDefinition definition, Stream workStream, IClientProxyContext context, CancellationToken cancellationToken = default)
    {
        var requestBuffer = new byte[64 * 1024];
        var read = await workStream.ReadAsync(requestBuffer, cancellationToken).ConfigureAwait(false);
        await using var local = await TcpProxyHandler.OpenLocalStreamAsync(definition.Config, cancellationToken).ConfigureAwait(false);
        await local.WriteAsync(requestBuffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);

        var responseBuffer = new byte[64 * 1024];
        var responseRead = await local.ReadAsync(responseBuffer, cancellationToken).ConfigureAwait(false);
        await workStream.WriteAsync(responseBuffer.AsMemory(0, responseRead), cancellationToken).ConfigureAwait(false);
        await workStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
