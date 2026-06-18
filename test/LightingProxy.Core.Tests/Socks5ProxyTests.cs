using System.Net;
using System.Net.Sockets;
using System.Text;
using LightingProxy.Core.Proxies;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;

namespace LightingProxy.Core.Tests;

public class Socks5ProxyTests
{
    [Fact]
    public void BuildMetadata_IncludesCredentials()
    {
        var handler = new Socks5ProxyHandler();
        var metadata = handler.BuildMetadata(new Socks5ProxyConfig
        {
            Name = "socks",
            LocalAddress = "127.0.0.1",
            LocalPort = 1080,
            Username = "user",
            Password = "pass"
        });

        Assert.Equal("user", metadata["username"]);
        Assert.Equal("pass", metadata["password"]);
    }

    [Fact]
    public async Task LocalSocks5Server_AcceptsNoAuthHandshake()
    {
        var port = Support.TestPorts.Allocate();
        using var cts = new CancellationTokenSource();
        _ = Socks5ProxyHandler.RunLocalSocks5ServerAsync(
            new IPEndPoint(IPAddress.Loopback, port),
            null,
            null,
            (_, _, _) => Task.FromResult<Stream>(new MemoryStream()),
            cts.Token);

        await Task.Delay(100);
        using var socket = await NetworkHelper.ConnectTcpAsync("127.0.0.1", port);
        await using var stream = NetworkHelper.CreateNetworkStream(socket, ownsSocket: true);
        await stream.WriteAsync(new byte[] { 0x05, 0x01, 0x00 });
        var response = new byte[2];
        await stream.ReadExactlyAsync(response);
        Assert.Equal(0x05, response[0]);
        Assert.Equal(0x00, response[1]);
        await cts.CancelAsync();
    }
}
