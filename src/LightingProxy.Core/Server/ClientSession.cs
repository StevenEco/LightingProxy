using System.Collections.Concurrent;
using System.Net.Sockets;
using LightingProxy.Core.Abstractions;
using LightingProxy.Core.Protocol;
using LightingProxy.Core.Proxies;
using LightingProxy.Core.Telemetry;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Server;

namespace LightingProxy.Core.Server;

internal sealed class ServerProxyContext : IServerProxyContext, IWorkConnectionRequester
{
    private readonly ClientSession _session;
    private readonly ConcurrentDictionary<string, ProxyDefinition> _secretProxies = new(StringComparer.OrdinalIgnoreCase);

    public ServerProxyContext(ClientSession session, ServerConfig config)
    {
        _session = session;
        BindAddress = config.ProxyBindAddress ?? config.BindAddress;
        HttpPort = config.Vhost?.HttpPort ?? 0;
        HttpsPort = config.Vhost?.HttpsPort ?? 0;
        TcpmuxPort = config.Vhost?.TcpmuxHttpConnectPort ?? 0;
    }

    public string BindAddress { get; }

    public ushort HttpPort { get; }

    public ushort HttpsPort { get; }

    public ushort TcpmuxPort { get; }

    public IWorkConnectionRequester WorkConnections => this;

    public void RegisterHttpRoute(string host, string proxyName)
        => _session.RegisterHttpRoute(host, proxyName);

    public void RegisterHttpsRoute(string host, string proxyName)
        => _session.RegisterHttpsRoute(host, proxyName);

    public void RegisterTcpmuxRoute(string host, string proxyName)
        => _session.RegisterTcpmuxRoute(host, proxyName);

    public bool TryGetSecretProxy(string proxyName, string secretKey, out ProxyDefinition? definition)
    {
        if (_secretProxies.TryGetValue(proxyName, out var proxy)
            && proxy.Metadata.TryGetValue("secretKey", out var expected)
            && expected == secretKey)
        {
            definition = proxy;
            return true;
        }

        definition = null;
        return false;
    }

    public void RegisterSecretProxy(ProxyDefinition definition)
        => _secretProxies[definition.Name] = definition;

    public Task<Stream> RequestWorkConnectionAsync(string proxyName, Dictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
        => _session.OpenWorkConnectionAsync(proxyName, metadata, cancellationToken);

    public ProxyRuntimeTracker Runtime => _session.Runtime;
}

public sealed class ClientSession
{
    private readonly Socket _controlSocket;
    private readonly Stream _controlStream;
    private readonly WorkConnectionBroker _broker = new();
    private readonly ProxyHandlerRegistry _registry;
    private readonly ServerConfig _config;
    private readonly Dictionary<string, ProxyDefinition> _proxies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _httpRoutes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _httpsRoutes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _tcpmuxRoutes = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private DateTimeOffset _lastHeartbeatUtc = DateTimeOffset.UtcNow;

    public string SessionId { get; } = Guid.NewGuid().ToString("N")[..8];

    public DateTimeOffset ConnectedAt { get; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastHeartbeatUtc => _lastHeartbeatUtc;

    public ProxyRuntimeTracker Runtime { get; } = new();

    public ClientSession(Socket controlSocket, Stream controlStream, ProxyHandlerRegistry registry, ServerConfig config)
    {
        _controlSocket = controlSocket;
        _controlStream = controlStream;
        _registry = registry;
        _config = config;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        var token = linked.Token;

        while (!token.IsCancellationRequested)
        {
            var message = await MessageSerializer.ReadAsync(_controlStream, token).ConfigureAwait(false);

            if (message is null)
            {
                break;
            }

            switch (message.Type)
            {
                case MessageType.NewProxy:
                    await HandleNewProxyAsync(message, token).ConfigureAwait(false);
                    break;
                case MessageType.NewVisitor:
                    await HandleNewVisitorAsync(message, token).ConfigureAwait(false);
                    break;
                case MessageType.Ping:
                    NotifyHeartbeatReceived();
                    await WriteControlAsync(MessageSerializer.CreatePong(), token).ConfigureAwait(false);
                    break;
            }
        }
    }

    private void NotifyHeartbeatReceived()
    {
        _lastHeartbeatUtc = DateTimeOffset.UtcNow;
    }

    private async Task WriteControlAsync(ControlMessage message, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await MessageSerializer.WriteAsync(_controlStream, message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void RegisterHttpRoute(string host, string proxyName) => _httpRoutes[host] = proxyName;

    public void RegisterHttpsRoute(string host, string proxyName) => _httpsRoutes[host] = proxyName;

    public void RegisterTcpmuxRoute(string host, string proxyName) => _tcpmuxRoutes[host] = proxyName;

    public bool TryResolveHttp(string host, out string proxyName) => _httpRoutes.TryGetValue(host, out proxyName!);

    public bool TryResolveHttps(string host, out string proxyName) => _httpsRoutes.TryGetValue(host, out proxyName!);

    public bool TryResolveTcpmux(string host, out string proxyName) => _tcpmuxRoutes.TryGetValue(host, out proxyName!);

    public async Task<Stream> OpenWorkConnectionAsync(string proxyName, Dictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid();
        await WriteControlAsync(MessageSerializer.CreateReqWorkConn(requestId, proxyName, metadata), cancellationToken).ConfigureAwait(false);
        var workStream = await _broker.WaitForWorkConnectionAsync(requestId, cancellationToken).ConfigureAwait(false);
        return workStream;
    }

    public void CompleteWorkConnection(Guid requestId, Stream workStream)
        => _broker.Complete(requestId, workStream);

    public bool TryCompleteWorkConnection(Guid requestId, Stream workStream)
    {
        if (_broker.HasPending(requestId))
        {
            CompleteWorkConnection(requestId, workStream);
            return true;
        }

        return false;
    }

    public bool TryResolveSecretProxy(string serverName, string secretKey, out string? proxyName)
    {
        if (_proxies.TryGetValue(serverName, out var definition)
            && definition.Metadata.TryGetValue("secretKey", out var expected)
            && expected == secretKey)
        {
            proxyName = serverName;
            return true;
        }

        proxyName = null;
        return false;
    }

    private async Task HandleNewProxyAsync(ControlMessage message, CancellationToken cancellationToken)
    {
        if (message.ProxyName is null || message.Protocol is null || message.Metadata is null)
        {
            await WriteControlAsync(MessageSerializer.CreateNewProxyResp(message.ProxyName ?? "unknown", false, "Invalid proxy registration."), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!Enum.TryParse<Domain.Enums.ProtocolType>(message.Protocol, true, out var protocol))
        {
            await WriteControlAsync(MessageSerializer.CreateNewProxyResp(message.ProxyName, false, "Unsupported protocol."), cancellationToken).ConfigureAwait(false);
            return;
        }

        var definition = new ProxyDefinition
        {
            Name = message.ProxyName,
            Protocol = protocol,
            Config = CreateConfigStub(protocol, message.ProxyName, message.Metadata),
            Metadata = new Dictionary<string, string>(message.Metadata, StringComparer.OrdinalIgnoreCase)
        };

        _proxies[definition.Name] = definition;
        var context = new ServerProxyContext(this, _config);

        if (protocol is Domain.Enums.ProtocolType.Stcp or Domain.Enums.ProtocolType.Xtcp)
        {
            context.RegisterSecretProxy(definition);
        }

        var handler = _registry.Get(protocol);
        _ = handler.StartServerAsync(definition, context, _cts.Token);

        await WriteControlAsync(MessageSerializer.CreateNewProxyResp(definition.Name, true), cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleNewVisitorAsync(ControlMessage message, CancellationToken cancellationToken)
    {
        var ok = message.ServerName is not null
                 && message.SecretKey is not null
                 && TryResolveSecretProxy(message.ServerName, message.SecretKey, out _);

        await WriteControlAsync(
            MessageSerializer.CreateNewVisitorResp(message.VisitorName ?? "visitor", ok, ok ? null : "Visitor authorization failed."),
            cancellationToken).ConfigureAwait(false);
    }

    private static ProxyConfigBase CreateConfigStub(Domain.Enums.ProtocolType protocol, string name, Dictionary<string, string> metadata)
    {
        var localAddress = metadata.GetValueOrDefault("localAddress", "127.0.0.1");
        var localPort = ushort.Parse(metadata.GetValueOrDefault("localPort", "0"));

        return protocol switch
        {
            Domain.Enums.ProtocolType.Udp => new UdpProxyConfig { Name = name, LocalAddress = localAddress, LocalPort = localPort },
            Domain.Enums.ProtocolType.Http => new HttpProxyConfig { Name = name, LocalAddress = localAddress, LocalPort = localPort },
            Domain.Enums.ProtocolType.Https => new HttpsProxyConfig { Name = name, LocalAddress = localAddress, LocalPort = localPort },
            Domain.Enums.ProtocolType.Stcp => new StcpProxyConfig { Name = name, LocalAddress = localAddress, LocalPort = localPort, SecretKey = metadata["secretKey"] },
            Domain.Enums.ProtocolType.Xtcp => new XtcpProxyConfig { Name = name, LocalAddress = localAddress, LocalPort = localPort, SecretKey = metadata["secretKey"] },
            Domain.Enums.ProtocolType.Socks5 => new Socks5ProxyConfig { Name = name, LocalAddress = localAddress, LocalPort = localPort },
            Domain.Enums.ProtocolType.Tcpmux => new TcpmuxProxyConfig { Name = name, LocalAddress = localAddress, LocalPort = localPort },
            Domain.Enums.ProtocolType.Kcp => new KcpProxyConfig { Name = name, LocalAddress = localAddress, LocalPort = localPort, RemotePort = ushort.Parse(metadata.GetValueOrDefault("remotePort", "0")) },
            _ => new TcpProxyConfig { Name = name, LocalAddress = localAddress, LocalPort = localPort }
        };
    }
}
