using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Core.Abstractions;

public sealed class ProxyDefinition
{
    public required string Name { get; init; }

    public required LightingProxy.Domain.Enums.ProtocolType Protocol { get; init; }

    public required ProxyConfigBase Config { get; init; }

    public Dictionary<string, string> Metadata { get; init; } = new();
}

public interface IWorkConnectionRequester
{
    Task<Stream> RequestWorkConnectionAsync(string proxyName, Dictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);
}

public interface IProxyHandler
{
    LightingProxy.Domain.Enums.ProtocolType Protocol { get; }

    Dictionary<string, string> BuildMetadata(ProxyConfigBase config);

    Task StartServerAsync(ProxyDefinition definition, IServerProxyContext context, CancellationToken cancellationToken = default);

    Task HandleClientWorkConnectionAsync(ProxyDefinition definition, Stream workStream, IClientProxyContext context, CancellationToken cancellationToken = default);
}

public interface IServerProxyContext
{
    string BindAddress { get; }

    ushort HttpPort { get; }

    ushort HttpsPort { get; }

    ushort TcpmuxPort { get; }

    IWorkConnectionRequester WorkConnections { get; }

    void RegisterHttpRoute(string host, string proxyName);

    void RegisterHttpsRoute(string host, string proxyName);

    void RegisterTcpmuxRoute(string host, string proxyName);

    bool TryGetSecretProxy(string proxyName, string secretKey, out ProxyDefinition? definition);
}

public interface IClientProxyContext
{
    string ServerAddress { get; }

    int ServerPort { get; }

    Task<Stream> OpenWorkConnectionAsync(CancellationToken cancellationToken = default);
}
