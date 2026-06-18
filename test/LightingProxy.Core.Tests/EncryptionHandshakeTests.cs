using System.Net;
using System.Net.Sockets;
using LightingProxy.Core.Protocol;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Common;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Transport;

namespace LightingProxy.Core.Tests;

public sealed class EncryptionHandshakeTests
{
    [Fact]
    public async Task LoginHandshake_Works_WithAes128Cfb()
    {
        var port = Support.TestPorts.Allocate();
        var listener = NetworkHelper.CreateTcpListener(new IPEndPoint(IPAddress.Loopback, port));
        const string token = "test-token";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverAccepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            serverAccepted.TrySetResult();
            var socket = await listener.AcceptAsync(cts.Token);
            await using var network = NetworkHelper.CreateNetworkStream(socket, ownsSocket: true);
            var transport = new ServerTransportConfig
            {
                Encryption = new TransportEncryptionConfig { Method = TransportEncryptionMethod.Aes128Cfb }
            };
            await using var stream = await TransportStreamFactory.WrapServerAsync(network, transport, token, cts.Token);
            var msg = await MessageSerializer.ReadAsync(stream, cts.Token);
            Assert.Equal(MessageType.Login, msg?.Type);
            await MessageSerializer.WriteAsync(stream, MessageSerializer.CreateLoginResp(true), cts.Token);
        }, cts.Token);

        await serverAccepted.Task.WaitAsync(cts.Token);
        using var clientSocket = await NetworkHelper.ConnectTcpAsync("127.0.0.1", port, cts.Token);
        await using var clientNetwork = NetworkHelper.CreateNetworkStream(clientSocket, ownsSocket: true);
        var clientTransport = new ClientTransportConfig
        {
            Encryption = new TransportEncryptionConfig { Method = TransportEncryptionMethod.Aes128Cfb }
        };
        await using var clientStream = await TransportStreamFactory.WrapClientAsync(
            clientNetwork, clientTransport, token, "127.0.0.1", cts.Token);
        await MessageSerializer.WriteAsync(clientStream, MessageSerializer.CreateLogin(token), cts.Token);
        var resp = await MessageSerializer.ReadAsync(clientStream, cts.Token);

        Assert.True(resp?.Success);
        await serverTask;
        listener.Dispose();
    }

    [Fact]
    public async Task LoginHandshake_Works_WithTls()
    {
        var port = Support.TestPorts.Allocate();
        var listener = NetworkHelper.CreateTcpListener(new IPEndPoint(IPAddress.Loopback, port));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverAccepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            serverAccepted.TrySetResult();
            var socket = await listener.AcceptAsync(cts.Token);
            await using var network = NetworkHelper.CreateNetworkStream(socket, ownsSocket: true);
            var transport = new ServerTransportConfig { Tls = new TlsConfig { Enabled = true } };
            await using var stream = await TransportStreamFactory.WrapServerAsync(network, transport, "test-token", cts.Token);
            var msg = await MessageSerializer.ReadAsync(stream, cts.Token);
            Assert.Equal(MessageType.Login, msg?.Type);
            await MessageSerializer.WriteAsync(stream, MessageSerializer.CreateLoginResp(true), cts.Token);
        }, cts.Token);

        await serverAccepted.Task.WaitAsync(cts.Token);
        using var clientSocket = await NetworkHelper.ConnectTcpAsync("127.0.0.1", port, cts.Token);
        await using var clientNetwork = NetworkHelper.CreateNetworkStream(clientSocket, ownsSocket: true);
        var clientTransport = new ClientTransportConfig { Tls = new TlsConfig { Enabled = true } };
        await using var clientStream = await TransportStreamFactory.WrapClientAsync(
            clientNetwork, clientTransport, "test-token", "127.0.0.1", cts.Token);
        await MessageSerializer.WriteAsync(clientStream, MessageSerializer.CreateLogin("test-token"), cts.Token);
        var resp = await MessageSerializer.ReadAsync(clientStream, cts.Token);

        Assert.True(resp?.Success);
        await serverTask;
        listener.Dispose();
    }
}
