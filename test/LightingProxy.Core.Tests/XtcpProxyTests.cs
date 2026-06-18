using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Client.Visitors;

namespace LightingProxy.Core.Tests;

public class XtcpProxyTests
{
    [Fact]
    public async Task XtcpProxy_ForwardsThroughVisitorTunnel()
    {
        var controlPort = Support.TestPorts.Allocate();
        var echoPort = Support.TestPorts.Allocate();
        var visitorPort = Support.TestPorts.Allocate();
        const string secret = "xtcp-secret";

        await using var echo = new Support.EchoTcpServer(echoPort);
        var clientConfig = Support.ProxyTestHarness.CreateClientConfig(controlPort, new XtcpProxyConfig
        {
            Name = "xtcp-echo",
            LocalAddress = "127.0.0.1",
            LocalPort = (ushort)echoPort,
            SecretKey = secret
        });
        clientConfig.Visitors =
        [
            new XtcpVisitorConfig
            {
                Name = "xtcp-visitor",
                ServerName = "xtcp-echo",
                SecretKey = secret,
                BindAddress = "127.0.0.1",
                BindPort = (ushort)visitorPort
            }
        ];

        await using var harness = new Support.ProxyTestHarness(
            Support.ProxyTestHarness.CreateServerConfig(controlPort),
            clientConfig);

        await harness.WaitReadyAsync();
        await Task.Delay(200);

        const string payload = "hello-xtcp";
        var response = await Support.TcpTestClient.SendAsync(visitorPort, payload);
        Assert.Equal(payload, response);
    }
}
