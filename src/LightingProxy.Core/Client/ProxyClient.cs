using System.Net;
using System.Net.Sockets;
using LightingProxy.Core.Abstractions;
using LightingProxy.Core.Protocol;
using LightingProxy.Core.Proxies;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Client.Visitors;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Core.Client;

public sealed class ProxyClient : IAsyncDisposable
{
    private readonly ClientConfig _config;
    private readonly ProxyHandlerRegistry _registry;
    private readonly Dictionary<string, ProxyDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _controlWriteLock = new(1, 1);
    private Socket? _controlSocket;
    private Stream? _controlStream;

    public Task ReadyTask => _readyTcs.Task;

    public ProxyClient(ClientConfig config, ProxyHandlerRegistry? registry = null)
    {
        _config = config;
        _registry = registry ?? ProxyHandlerRegistry.CreateDefault();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        var token = linked.Token;

        _controlSocket = await NetworkHelper.ConnectTcpAsync(_config.ServerAddress, _config.ServerPort, token).ConfigureAwait(false);
        var networkStream = NetworkHelper.CreateNetworkStream(_controlSocket, ownsSocket: false);
        _controlStream = await TransportStreamFactory.WrapClientAsync(
            networkStream,
            _config.Transport,
            _config.Auth.Token,
            _config.ServerAddress,
            token).ConfigureAwait(false);

        await MessageSerializer.WriteAsync(_controlStream, MessageSerializer.CreateLogin(_config.Auth.Token ?? string.Empty), token).ConfigureAwait(false);
        var loginResp = await MessageSerializer.ReadAsync(_controlStream, token).ConfigureAwait(false);

        if (loginResp?.Success != true)
        {
            throw new InvalidOperationException(loginResp?.Error ?? "Login failed.");
        }

        await RegisterProxiesAsync(token).ConfigureAwait(false);
        await RegisterVisitorsAsync(token).ConfigureAwait(false);

        var heartbeat = new ControlHeartbeat(
            _config.Transport.HeartbeatIntervalSeconds,
            _config.Transport.HeartbeatTimeoutSeconds,
            SendPingAsync);
        heartbeat.Reset();

        _readyTcs.TrySetResult();

        await Task.WhenAll(
            heartbeat.RunAsync(token),
            ListenControlAsync(token, heartbeat)).ConfigureAwait(false);
    }

    private async Task SendPingAsync(CancellationToken cancellationToken)
    {
        await WriteControlAsync(MessageSerializer.CreatePing(), cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteControlAsync(ControlMessage message, CancellationToken cancellationToken)
    {
        await _controlWriteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await MessageSerializer.WriteAsync(_controlStream!, message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _controlWriteLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        _controlStream?.Dispose();
        _controlSocket?.Dispose();
        _cts.Dispose();
    }

    private async Task RegisterProxiesAsync(CancellationToken cancellationToken)
    {
        if (_config.Proxies is null)
        {
            return;
        }

        foreach (var proxy in _config.Proxies.Where(p => p.Enabled))
        {
            var handler = _registry.Get(proxy.Type);
            var metadata = handler.BuildMetadata(proxy);
            var definition = new ProxyDefinition
            {
                Name = proxy.Name,
                Protocol = proxy.Type,
                Config = proxy,
                Metadata = metadata
            };

            _definitions[definition.Name] = definition;

            await WriteControlAsync(
                MessageSerializer.CreateNewProxy(definition.Name, definition.Protocol.ToString(), metadata),
                cancellationToken).ConfigureAwait(false);

            var response = await MessageSerializer.ReadAsync(_controlStream!, cancellationToken).ConfigureAwait(false);
            if (response?.Success != true)
            {
                throw new InvalidOperationException(response?.Error ?? $"Failed to register proxy '{definition.Name}'.");
            }

            if (proxy.Type == Domain.Enums.ProtocolType.Socks5)
            {
                var socks = (Socks5ProxyConfig)proxy;
                _ = Socks5ProxyHandler.RunLocalSocks5ServerAsync(
                    new IPEndPoint(IPAddress.Parse(proxy.LocalAddress), proxy.LocalPort),
                    socks.Username,
                    socks.Password,
                    (host, port, ct) => OpenSocksTunnelAsync(definition.Name, host, port, ct),
                    _cts.Token);
            }
        }
    }

    private async Task RegisterVisitorsAsync(CancellationToken cancellationToken)
    {
        if (_config.Visitors is null)
        {
            return;
        }

        foreach (var visitor in _config.Visitors.Where(v => v.Enabled))
        {
            await WriteControlAsync(
                MessageSerializer.CreateNewVisitor(visitor.Name, visitor.ServerName, visitor.SecretKey),
                cancellationToken).ConfigureAwait(false);

            var response = await MessageSerializer.ReadAsync(_controlStream!, cancellationToken).ConfigureAwait(false);
            if (response?.Success != true)
            {
                throw new InvalidOperationException(response?.Error ?? $"Failed to register visitor '{visitor.Name}'.");
            }

            var endpoint = new IPEndPoint(IPAddress.Parse(visitor.BindAddress), visitor.BindPort);
            switch (visitor)
            {
                case StcpVisitorConfig:
                    _ = StcpProxyHandler.RunVisitorAsync(
                        endpoint,
                        visitor.ServerName,
                        visitor.SecretKey,
                        OpenVisitorTunnelAsync,
                        _cts.Token);
                    break;
                case XtcpVisitorConfig:
                    _ = XtcpProxyHandler.RunVisitorAsync(
                        endpoint,
                        visitor.ServerName,
                        visitor.SecretKey,
                        OpenVisitorTunnelAsync,
                        _cts.Token);
                    break;
            }
        }
    }

    private async Task ListenControlAsync(CancellationToken cancellationToken, ControlHeartbeat heartbeat)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await MessageSerializer.ReadAsync(_controlStream!, cancellationToken).ConfigureAwait(false);
            if (message is null)
            {
                break;
            }

            switch (message.Type)
            {
                case MessageType.Pong:
                    heartbeat.NotifyPongReceived();
                    break;
                case MessageType.ReqWorkConn when message.ProxyName is not null:
                    _ = HandleWorkConnectionRequestAsync(message, cancellationToken);
                    break;
            }
        }
    }

    private async Task HandleWorkConnectionRequestAsync(ControlMessage message, CancellationToken cancellationToken)
    {
        var workStream = await TransportStreamFactory.ConnectClientAsync(
            _config.ServerAddress,
            _config.ServerPort,
            _config.Transport,
            _config.Auth.Token,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await MessageSerializer.WriteAsync(workStream, MessageSerializer.CreateWorkConnReady(message.RequestId), cancellationToken).ConfigureAwait(false);

            if (!_definitions.TryGetValue(message.ProxyName!, out var definition))
            {
                throw new InvalidOperationException($"Unknown proxy '{message.ProxyName}'.");
            }

            var handler = _registry.Get(definition.Protocol);
            var context = new ClientProxyContext(_config.ServerAddress, _config.ServerPort);
            await handler.HandleClientWorkConnectionAsync(definition, workStream, context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await workStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<Stream> OpenVisitorTunnelAsync(string serverName, string secretKey, CancellationToken cancellationToken)
    {
        var workStream = await TransportStreamFactory.ConnectClientAsync(
            _config.ServerAddress,
            _config.ServerPort,
            _config.Transport,
            _config.Auth.Token,
            cancellationToken).ConfigureAwait(false);
        await MessageSerializer.WriteAsync(
            workStream,
            new ControlMessage { Type = MessageType.OpenVisitorTunnel, ServerName = serverName, SecretKey = secretKey },
            cancellationToken).ConfigureAwait(false);
        return workStream;
    }

    private async Task<Stream> OpenSocksTunnelAsync(string proxyName, string host, int port, CancellationToken cancellationToken)
    {
        var workStream = await TransportStreamFactory.ConnectClientAsync(
            _config.ServerAddress,
            _config.ServerPort,
            _config.Transport,
            _config.Auth.Token,
            cancellationToken).ConfigureAwait(false);
        var requestId = Guid.NewGuid();
        await MessageSerializer.WriteAsync(workStream, MessageSerializer.CreateWorkConnReady(requestId), cancellationToken).ConfigureAwait(false);
        var start = await MessageSerializer.ReadAsync(workStream, cancellationToken).ConfigureAwait(false);
        if (start?.ProxyName is null)
        {
            await workStream.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("SOCKS tunnel failed.");
        }

        var hostBytes = System.Text.Encoding.ASCII.GetBytes(host);
        var connectRequest = new byte[7 + hostBytes.Length];
        connectRequest[0] = 0x05;
        connectRequest[1] = 0x01;
        connectRequest[2] = 0x00;
        connectRequest[3] = 0x03;
        connectRequest[4] = (byte)hostBytes.Length;
        hostBytes.CopyTo(connectRequest, 5);
        connectRequest[5 + hostBytes.Length] = (byte)(port >> 8);
        connectRequest[6 + hostBytes.Length] = (byte)(port & 0xFF);
        await workStream.WriteAsync(connectRequest, cancellationToken).ConfigureAwait(false);
        return workStream;
    }
}

internal sealed class ClientProxyContext : IClientProxyContext
{
    public ClientProxyContext(string serverAddress, int serverPort)
    {
        ServerAddress = serverAddress;
        ServerPort = serverPort;
    }

    public string ServerAddress { get; }

    public int ServerPort { get; }

    public Task<Stream> OpenWorkConnectionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Work connection is already established.");
}
