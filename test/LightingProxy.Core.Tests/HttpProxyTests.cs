using LightingProxy.Domain.Client.Proxies;

namespace LightingProxy.Core.Tests;

public class HttpProxyTests
{
    [Fact]
    public async Task HttpProxy_RoutesByHostHeader()
    {
        var controlPort = Support.TestPorts.Allocate();
        var echoPort = Support.TestPorts.Allocate();
        var httpPort = Support.TestPorts.Allocate();

        await using var echo = new Support.EchoTcpServer(echoPort);
        await using var harness = new Support.ProxyTestHarness(
            Support.ProxyTestHarness.CreateServerConfig(controlPort, httpPort: httpPort),
            Support.ProxyTestHarness.CreateClientConfig(controlPort, new HttpProxyConfig
            {
                Name = "http-echo",
                LocalAddress = "127.0.0.1",
                LocalPort = (ushort)echoPort,
                CustomDomains = ["echo.test"]
            }));

        await harness.WaitReadyAsync();

        const string payload = "GET / HTTP/1.1\r\nHost: echo.test\r\n\r\nbody";
        var response = await Support.TcpTestClient.SendAsync(httpPort, payload);

        Assert.Contains("body", response);
    }
}
