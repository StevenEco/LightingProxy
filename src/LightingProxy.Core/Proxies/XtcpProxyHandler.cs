using System.Net;
using System.Net.Sockets;
using LightingProxy.Core.Abstractions;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Core.Proxies;

public sealed class XtcpProxyHandler : IProxyHandler
{
    public LightingProxy.Domain.Enums.ProtocolType Protocol => LightingProxy.Domain.Enums.ProtocolType.Xtcp;

    public Dictionary<string, string> BuildMetadata(ProxyConfigBase config)
        => ProxyMetadataBuilder.Build(config);

    public Task StartServerAsync(ProxyDefinition definition, IServerProxyContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task HandleClientWorkConnectionAsync(ProxyDefinition definition, Stream workStream, IClientProxyContext context, CancellationToken cancellationToken = default)
    {
        await using var local = await TcpProxyHandler.OpenLocalStreamAsync(definition.Config, cancellationToken).ConfigureAwait(false);
        await StreamRelay.RelayBidirectionalAsync(workStream, local, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task RunVisitorAsync(
        IPEndPoint bindEndpoint,
        string serverName,
        string secretKey,
        Func<string, string, CancellationToken, Task<Stream>> visitorConnectionFactory,
        CancellationToken cancellationToken)
        => await StcpProxyHandler.RunVisitorAsync(bindEndpoint, serverName, secretKey, visitorConnectionFactory, cancellationToken).ConfigureAwait(false);
}
