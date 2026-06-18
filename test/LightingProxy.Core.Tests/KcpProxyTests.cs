using System.Net;
using System.Text;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;

namespace LightingProxy.Core.Tests;

public class KcpProxyTests
{
    [Fact]
    public async Task KcpProxy_ForwardsPacketThroughServer()
    {
        var controlPort = Support.TestPorts.Allocate();
        var echoPort = Support.TestPorts.Allocate();
        var remotePort = Support.TestPorts.Allocate();

        await using var echo = new Support.EchoTcpServer(echoPort);
        await using var harness = new Support.ProxyTestHarness(
            Support.ProxyTestHarness.CreateServerConfig(controlPort),
            Support.ProxyTestHarness.CreateClientConfig(controlPort, new KcpProxyConfig
            {
                Name = "kcp-echo",
                LocalAddress = "127.0.0.1",
                LocalPort = (ushort)echoPort,
                RemotePort = (ushort)remotePort
            }));

        await harness.WaitReadyAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var channel = new KcpChannel(new IPEndPoint(IPAddress.Loopback, 0));
        var payload = "hello-kcp"u8.ToArray();
        await channel.SendAsync(new IPEndPoint(IPAddress.Loopback, remotePort), payload, cts.Token);

        var received = await channel.ReceiveAsync(cts.Token);
        Assert.NotNull(received);
        Assert.Equal(payload, received.Value.Payload);
    }
}
