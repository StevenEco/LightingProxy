using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;

namespace LightingProxy.Core.Tests;

public class HttpsProxyTests
{
    [Fact]
    public void SniParser_ExtractsServerName()
        => Support.TlsTestHelper.AssertParsesServerName("secure.test");

    [Fact]
    public async Task HttpsProxy_RoutesBySni()
    {
        var controlPort = Support.TestPorts.Allocate();
        var echoPort = Support.TestPorts.Allocate();
        var httpsPort = Support.TestPorts.Allocate();

        await using var echo = new Support.EchoTcpServer(echoPort);
        await using var harness = new Support.ProxyTestHarness(
            Support.ProxyTestHarness.CreateServerConfig(controlPort, httpsPort: httpsPort),
            Support.ProxyTestHarness.CreateClientConfig(controlPort, new HttpsProxyConfig
            {
                Name = "https-echo",
                LocalAddress = "127.0.0.1",
                LocalPort = (ushort)echoPort,
                CustomDomains = ["secure.test"]
            }));

        await harness.WaitReadyAsync();

        var hello = Support.TlsTestHelper.BuildClientHello("secure.test");
        var response = await SendRawAsync(httpsPort, hello);
        Assert.True(response.Length >= hello.Length);
        Assert.Equal(hello, response.AsSpan(0, hello.Length).ToArray());
    }

    private static async Task<byte[]> SendRawAsync(int port, byte[] payload)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var socket = await NetworkHelper.ConnectTcpAsync("127.0.0.1", port, cts.Token);
        await using var stream = NetworkHelper.CreateNetworkStream(socket, ownsSocket: true);
        await stream.WriteAsync(payload, cts.Token);
        var buffer = new byte[4096];
        var read = await stream.ReadAsync(buffer, cts.Token);
        return buffer.AsSpan(0, read).ToArray();
    }
}
