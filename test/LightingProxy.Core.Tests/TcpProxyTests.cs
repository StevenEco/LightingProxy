using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;

namespace LightingProxy.Core.Tests;

public class TcpProxyTests
{
    [Fact]
    public async Task TcpProxy_ForwardsTrafficThroughServer()
    {
        var controlPort = Support.TestPorts.Allocate();
        var echoPort = Support.TestPorts.Allocate();
        var remotePort = Support.TestPorts.Allocate();

        await using var echo = new Support.EchoTcpServer(echoPort);
        await using var harness = new Support.ProxyTestHarness(
            Support.ProxyTestHarness.CreateServerConfig(controlPort),
            Support.ProxyTestHarness.CreateClientConfig(controlPort, new TcpProxyConfig
            {
                Name = "tcp-echo",
                LocalAddress = "127.0.0.1",
                LocalPort = (ushort)echoPort,
                RemotePort = (ushort)remotePort
            }));

        await harness.WaitReadyAsync();

        const string payload = "hello-tcp";
        var response = await Support.TcpTestClient.SendAsync(remotePort, payload);

        Assert.Equal(payload, response);
    }
}
