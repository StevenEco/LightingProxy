using System.Net;
using System.Net.Sockets;
using System.Text;
using LightingProxy.Core.Client;
using LightingProxy.Core.Server;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Client.Visitors;
using LightingProxy.Domain.Common;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Server;
using LightingProxy.Domain.Transport;

namespace LightingProxy.Core.Tests.Support;

internal static class TestPorts
{
    public static int Allocate()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

internal sealed class EchoTcpServer : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Socket _listener;

    public EchoTcpServer(int port)
    {
        Port = port;
        _listener = NetworkHelper.CreateTcpListener(new IPEndPoint(IPAddress.Loopback, port));
        _ = AcceptLoopAsync(_cts.Token);
    }

    public int Port { get; }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Dispose();
        _cts.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var socket = await _listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                _ = HandleClientAsync(socket, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static async Task HandleClientAsync(Socket socket, CancellationToken cancellationToken)
    {
        await using var stream = NetworkHelper.CreateNetworkStream(socket);
        var buffer = new byte[4096];

        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }
}

internal sealed class EchoUdpServer : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Socket _socket;

    public EchoUdpServer(int port)
    {
        Port = port;
        _socket = NetworkHelper.CreateUdpSocket(new IPEndPoint(IPAddress.Loopback, port));
        _ = LoopAsync(_cts.Token);
    }

    public int Port { get; }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _socket.Dispose();
        _cts.Dispose();
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
        var result = await NetworkHelper.ReceiveUdpAsync(_socket, buffer, cancellationToken).ConfigureAwait(false);
        await NetworkHelper.SendUdpAsync(_socket, buffer.AsSpan(0, result.ReceivedBytes), result.RemoteEndPoint, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

internal sealed class ProxyTestHarness : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ProxyServer _server;
    private readonly ProxyClient _client;
    private readonly Task _serverTask;
    private readonly Task _clientTask;

    public ProxyTestHarness(ServerConfig serverConfig, ClientConfig clientConfig)
    {
        _server = new ProxyServer(serverConfig);
        _client = new ProxyClient(clientConfig);
        _serverTask = _server.StartAsync(_cts.Token);
        _clientTask = Task.Run(async () =>
        {
            await Task.Delay(50, _cts.Token).ConfigureAwait(false);
            await _client.StartAsync(_cts.Token).ConfigureAwait(false);
        }, _cts.Token);
    }

    public ProxyServer Server => _server;

    public ProxyClient Client => _client;

    public Task ClientTask => _clientTask;

    public Task ServerTask => _serverTask;

    public static ServerConfig CreateServerConfig(int controlPort, int? httpPort = null, int? httpsPort = null, int? tcpmuxPort = null)
        => CreateServerConfig(controlPort, httpPort, httpsPort, tcpmuxPort, tlsEnabled: false, encryption: TransportEncryptionMethod.None);

    public static ServerConfig CreateServerConfig(int controlPort, TransportEncryptionMethod encryption)
        => CreateServerConfig(controlPort, null, null, null, tlsEnabled: false, encryption: encryption);

    public static ServerConfig CreateServerConfig(int controlPort, bool tlsEnabled)
        => CreateServerConfig(controlPort, null, null, null, tlsEnabled, TransportEncryptionMethod.None);

    public static ServerConfig CreateServerConfig(
        int controlPort,
        int? httpPort,
        int? httpsPort,
        int? tcpmuxPort,
        bool tlsEnabled,
        TransportEncryptionMethod encryption)
        => new()
        {
            BindAddress = "127.0.0.1",
            BindPort = (ushort)controlPort,
            Auth = new AuthConfig { Method = AuthMethod.Token, Token = "test-token" },
            Transport = new ServerTransportConfig
            {
                Tls = new TlsConfig { Enabled = tlsEnabled },
                Encryption = new TransportEncryptionConfig { Method = encryption }
            },
            Vhost = new VhostConfig
            {
                HttpPort = (ushort)(httpPort ?? 0),
                HttpsPort = (ushort)(httpsPort ?? 0),
                TcpmuxHttpConnectPort = (ushort)(tcpmuxPort ?? 0)
            }
        };

    public static ClientConfig CreateClientConfig(int controlPort, params ProxyConfigBase[] proxies)
        => CreateClientConfig(controlPort, tlsEnabled: false, encryption: TransportEncryptionMethod.None, proxies);

    public static ClientConfig CreateClientConfig(int controlPort, TransportEncryptionMethod encryption, params ProxyConfigBase[] proxies)
        => CreateClientConfig(controlPort, tlsEnabled: false, encryption: encryption, proxies);

    public static ClientConfig CreateClientConfig(
        int controlPort,
        bool tlsEnabled,
        TransportEncryptionMethod encryption,
        params ProxyConfigBase[] proxies)
        => new()
        {
            ServerAddress = "127.0.0.1",
            ServerPort = (ushort)controlPort,
            Auth = new AuthConfig { Method = AuthMethod.Token, Token = "test-token" },
            Transport = new ClientTransportConfig
            {
                Tls = new TlsConfig { Enabled = tlsEnabled },
                Encryption = new TransportEncryptionConfig { Method = encryption }
            },
            Proxies = proxies
        };

    public async Task WaitReadyAsync()
    {
        await _client.ReadyTask.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        await _client.DisposeAsync();
        await _server.DisposeAsync();
        _cts.Dispose();
        try
        {
            await Task.WhenAll(_serverTask, _clientTask).WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Background tasks may end with cancellation or socket errors.
        }
    }
}

internal static class TcpTestClient
{
    public static async Task<string> SendAsync(int port, string payload)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var socket = await NetworkHelper.ConnectTcpAsync("127.0.0.1", port, cts.Token);
        await using var stream = NetworkHelper.CreateNetworkStream(socket, ownsSocket: true);
        var bytes = Encoding.ASCII.GetBytes(payload);
        await stream.WriteAsync(bytes, cts.Token);
        await stream.FlushAsync(cts.Token);

        var buffer = new byte[4096];
        var read = await stream.ReadAsync(buffer, cts.Token);
        return Encoding.ASCII.GetString(buffer, 0, read);
    }
}

internal static class UdpTestClient
{
    public static async Task<string> SendAsync(int port, string payload)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var socket = NetworkHelper.CreateUdpSocket(new IPEndPoint(IPAddress.Loopback, 0));
        var bytes = Encoding.ASCII.GetBytes(payload);
        var remote = new IPEndPoint(IPAddress.Loopback, port);
        await NetworkHelper.SendUdpAsync(socket, bytes, remote, cts.Token);
        var buffer = new byte[4096];
        var result = await NetworkHelper.ReceiveUdpAsync(socket, buffer, cts.Token);
        return Encoding.ASCII.GetString(buffer, 0, result.ReceivedBytes);
    }
}
