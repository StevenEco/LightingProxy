using System.Net;
using System.Text;
using LightingProxy.Core.Protocol;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Client.Visitors;

namespace LightingProxy.Core.Tests;

public class StcpProxyTests
{
    [Fact]
    public async Task StcpProxy_ForwardsThroughSecretTunnel()
    {
        var controlPort = Support.TestPorts.Allocate();
        var echoPort = Support.TestPorts.Allocate();
        var visitorPort = Support.TestPorts.Allocate();
        const string secret = "secret-key";

        await using var echo = new Support.EchoTcpServer(echoPort);
        var clientConfig = Support.ProxyTestHarness.CreateClientConfig(controlPort, new StcpProxyConfig
        {
            Name = "secret-echo",
            LocalAddress = "127.0.0.1",
            LocalPort = (ushort)echoPort,
            SecretKey = secret
        });
        clientConfig.Visitors =
        [
            new StcpVisitorConfig
            {
                Name = "visitor",
                ServerName = "secret-echo",
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

        const string payload = "hello-stcp";
        var response = await Support.TcpTestClient.SendAsync(visitorPort, payload);
        Assert.Equal(payload, response);
    }

    [Fact]
    public async Task OpenVisitorTunnel_RejectsInvalidSecret()
    {
        var controlPort = Support.TestPorts.Allocate();
        var echoPort = Support.TestPorts.Allocate();

        await using var echo = new Support.EchoTcpServer(echoPort);
        await using var harness = new Support.ProxyTestHarness(
            Support.ProxyTestHarness.CreateServerConfig(controlPort),
            Support.ProxyTestHarness.CreateClientConfig(controlPort, new StcpProxyConfig
            {
                Name = "secret-echo",
                LocalAddress = "127.0.0.1",
                LocalPort = (ushort)echoPort,
                SecretKey = "real-secret"
            }));

        await harness.WaitReadyAsync();

        using var socket = await NetworkHelper.ConnectTcpAsync("127.0.0.1", controlPort);
        await using var stream = NetworkHelper.CreateNetworkStream(socket, ownsSocket: true);
        await MessageSerializer.WriteAsync(stream, new ControlMessage
        {
            Type = MessageType.OpenVisitorTunnel,
            ServerName = "secret-echo",
            SecretKey = "wrong-secret"
        });

        var buffer = new byte[16];
        var read = await stream.ReadAsync(buffer);
        Assert.Equal(0, read);
    }
}
