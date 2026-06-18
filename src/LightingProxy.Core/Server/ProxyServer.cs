using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using LightingProxy.Core.Abstractions;
using LightingProxy.Core.Protocol;
using LightingProxy.Core.Proxies;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Server;

namespace LightingProxy.Core.Server;

public sealed class ProxyServer : IAsyncDisposable
{
    private readonly ServerConfig _config;
    private readonly ProxyHandlerRegistry _registry;
    private readonly List<ClientSession> _sessions = [];
    private readonly CancellationTokenSource _cts = new();
    private Socket? _controlListener;
    private Task? _httpTask;
    private Task? _httpsTask;
    private Task? _tcpmuxTask;

    public ProxyServer(ServerConfig config, ProxyHandlerRegistry? registry = null)
    {
        _config = config;
        _registry = registry ?? ProxyHandlerRegistry.CreateDefault();
    }

    public int Port => _config.BindPort;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        var token = linked.Token;

        _controlListener = NetworkHelper.CreateTcpListener(new IPEndPoint(IPAddress.Loopback, _config.BindPort));
        StartSharedListeners(token);

        while (!token.IsCancellationRequested)
        {
            var socket = await _controlListener.AcceptAsync(token).ConfigureAwait(false);
            _ = HandleIncomingSocketAsync(socket, token);
        }
    }

    public ClientSession? GetFirstSession() => _sessions.FirstOrDefault();

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        _controlListener?.Dispose();

        foreach (var task in new[] { _httpTask, _httpsTask, _tcpmuxTask })
        {
            if (task is null)
            {
                continue;
            }

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Listener tasks exit when the server is stopped.
            }
        }

        _cts.Dispose();
    }

    private void StartSharedListeners(CancellationToken cancellationToken)
    {
        if (_config.Vhost?.HttpPort > 0)
        {
            _httpTask = HttpProxyHandler.RunHttpListenerAsync(
                _config.Vhost.HttpPort,
                (host, ct) => ResolveWorkStreamAsync(host, _sessions, s => s.TryResolveHttp(host, out var proxy) ? proxy : null, ct),
                cancellationToken);
        }

        if (_config.Vhost?.HttpsPort > 0)
        {
            _httpsTask = HttpsProxyHandler.RunHttpsListenerAsync(
                _config.Vhost.HttpsPort,
                (host, ct) => ResolveWorkStreamAsync(host, _sessions, s => s.TryResolveHttps(host, out var proxy) ? proxy : null, ct),
                cancellationToken);
        }

        if (_config.Vhost?.TcpmuxHttpConnectPort > 0)
        {
            _tcpmuxTask = TcpmuxProxyHandler.RunTcpmuxListenerAsync(
                _config.Vhost.TcpmuxHttpConnectPort,
                (host, ct) => ResolveWorkStreamAsync(host, _sessions, s => s.TryResolveTcpmux(host, out var proxy) ? proxy : null, ct),
                cancellationToken);
        }
    }

    private static async Task<Stream> ResolveWorkStreamAsync(
        string host,
        IReadOnlyList<ClientSession> sessions,
        Func<ClientSession, string?> resolver,
        CancellationToken cancellationToken)
    {
        foreach (var session in sessions)
        {
            var proxyName = resolver(session);
            if (proxyName is not null)
            {
                return await session.OpenWorkConnectionAsync(proxyName, null, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"No proxy registered for host '{host}'.");
    }

    private async Task HandleIncomingSocketAsync(Socket socket, CancellationToken cancellationToken)
    {
        var networkStream = NetworkHelper.CreateNetworkStream(socket, ownsSocket: true);
        Stream stream;
        try
        {
            stream = await TransportStreamFactory.WrapServerAsync(
                networkStream,
                _config.Transport,
                _config.Auth.Token,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await networkStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        var firstMessage = await MessageSerializer.ReadAsync(stream, cancellationToken).ConfigureAwait(false);

        if (firstMessage?.Type == MessageType.WorkConnReady)
        {
            foreach (var session in _sessions)
            {
                if (session.TryCompleteWorkConnection(firstMessage.RequestId, stream))
                {
                    return;
                }
            }

            await stream.DisposeAsync().ConfigureAwait(false);
            return;
        }

        if (firstMessage?.Type == MessageType.OpenVisitorTunnel)
        {
            await HandleVisitorTunnelAsync(stream, firstMessage, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using (stream)
        {
            if (firstMessage?.Type == MessageType.Login)
            {
                if (firstMessage.Token != _config.Auth.Token)
                {
                    await MessageSerializer.WriteAsync(stream, MessageSerializer.CreateLoginResp(false, "Invalid token."), cancellationToken).ConfigureAwait(false);
                    return;
                }

                await MessageSerializer.WriteAsync(stream, MessageSerializer.CreateLoginResp(true), cancellationToken).ConfigureAwait(false);
                var session = new ClientSession(socket, stream, _registry, _config);
                _sessions.Add(session);
                await session.RunAsync(cancellationToken).ConfigureAwait(false);
                _sessions.Remove(session);
            }
        }
    }

    private async Task HandleVisitorTunnelAsync(Stream visitorStream, ControlMessage message, CancellationToken cancellationToken)
    {
        if (message.ServerName is null || message.SecretKey is null)
        {
            visitorStream.Dispose();
            return;
        }

        var session = _sessions.FirstOrDefault(s => s.TryResolveSecretProxy(message.ServerName, message.SecretKey, out _));
        if (session is null)
        {
            visitorStream.Dispose();
            return;
        }

        try
        {
            await using var proxyStream = await session.OpenWorkConnectionAsync(message.ServerName, new Dictionary<string, string> { ["visitor"] = "true" }, cancellationToken).ConfigureAwait(false);
            await StreamRelay.RelayBidirectionalAsync(visitorStream, proxyStream, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            visitorStream.Dispose();
        }
    }
}
