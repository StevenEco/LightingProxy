using LightingProxy.Domain.Client.Proxies;

namespace LightingProxy.Core.Tests;

public class UdpProxyTests
{
    [Fact]
    public async Task UdpProxy_ForwardsDatagramThroughServer()
    {
        var controlPort = Support.TestPorts.Allocate();
        var echoPort = Support.TestPorts.Allocate();
        var remotePort = Support.TestPorts.Allocate();

        await using var echo = new Support.EchoUdpServer(echoPort);
        await using var harness = new Support.ProxyTestHarness(
            Support.ProxyTestHarness.CreateServerConfig(controlPort),
            Support.ProxyTestHarness.CreateClientConfig(controlPort, new UdpProxyConfig
            {
                Name = "udp-echo",
                LocalAddress = "127.0.0.1",
                LocalPort = (ushort)echoPort,
                RemotePort = (ushort)remotePort
            }));

        await harness.WaitReadyAsync();

        const string payload = "hello-udp";
        var response = await Support.UdpTestClient.SendAsync(remotePort, payload);

        Assert.Equal(payload, response);
    }
}
