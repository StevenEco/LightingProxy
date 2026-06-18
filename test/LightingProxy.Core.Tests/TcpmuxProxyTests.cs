using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;

namespace LightingProxy.Core.Tests;

public class TcpmuxProxyTests
{
    [Fact]
    public async Task TcpmuxProxy_HandlesHttpConnect()
    {
        var controlPort = Support.TestPorts.Allocate();
        var echoPort = Support.TestPorts.Allocate();
        var tcpmuxPort = Support.TestPorts.Allocate();

        await using var echo = new Support.EchoTcpServer(echoPort);
        await using var harness = new Support.ProxyTestHarness(
            Support.ProxyTestHarness.CreateServerConfig(controlPort, tcpmuxPort: tcpmuxPort),
            Support.ProxyTestHarness.CreateClientConfig(controlPort, new TcpmuxProxyConfig
            {
                Name = "mux-echo",
                LocalAddress = "127.0.0.1",
                LocalPort = (ushort)echoPort,
                CustomDomains = ["mux.test"],
                Multiplexer = "httpconnect"
            }));

        await harness.WaitReadyAsync();

        const string payload = "CONNECT mux.test:80 HTTP/1.1\r\nHost: mux.test\r\n\r\npayload";
        var response = await SendUntilContainsAsync(tcpmuxPort, payload, "payload");
        Assert.Contains("payload", response);
    }

    private static async Task<string> SendUntilContainsAsync(int port, string payload, string expected)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var socket = await NetworkHelper.ConnectTcpAsync("127.0.0.1", port, cts.Token);
        await using var stream = NetworkHelper.CreateNetworkStream(socket, ownsSocket: true);
        var bytes = System.Text.Encoding.ASCII.GetBytes(payload);
        await stream.WriteAsync(bytes, cts.Token);
        await stream.FlushAsync(cts.Token);

        var builder = new System.Text.StringBuilder();
        var buffer = new byte[4096];
        while (builder.Length < 64 * 1024 && !cts.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer, cts.Token);
            if (read == 0)
            {
                break;
            }

            builder.Append(System.Text.Encoding.ASCII.GetString(buffer, 0, read));
            if (builder.ToString().Contains(expected, StringComparison.Ordinal))
            {
                break;
            }
        }

        return builder.ToString();
    }
}
