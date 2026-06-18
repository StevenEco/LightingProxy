using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Common;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Transport;

namespace LightingProxy.Core.Tests;

public sealed class EncryptionTests
{
    [Theory]
    [InlineData(TransportEncryptionMethod.Aes128Cfb)]
    [InlineData(TransportEncryptionMethod.ChaCha20Poly1305)]
    [InlineData(TransportEncryptionMethod.Aes256Gcm)]
    public async Task TcpProxy_Works_WithApplicationEncryption(TransportEncryptionMethod method)
    {
        var controlPort = Support.TestPorts.Allocate();
        var echoPort = Support.TestPorts.Allocate();
        await using var echo = new Support.EchoTcpServer(echoPort);

        var proxy = new TcpProxyConfig
        {
            Name = "tcp-encrypted",
            Type = ProtocolType.Tcp,
            LocalPort = (ushort)echoPort,
            RemotePort = (ushort)Support.TestPorts.Allocate()
        };

        await using var harness = new Support.ProxyTestHarness(
            Support.ProxyTestHarness.CreateServerConfig(controlPort, encryption: method),
            Support.ProxyTestHarness.CreateClientConfig(controlPort, encryption: method, proxy));

        await harness.WaitReadyAsync();
        var response = await Support.TcpTestClient.SendAsync(proxy.RemotePort, "encrypted-payload");
        Assert.Equal("encrypted-payload", response);
    }

    [Fact]
    public async Task TcpProxy_Works_WithTls()
    {
        var controlPort = Support.TestPorts.Allocate();
        var echoPort = Support.TestPorts.Allocate();
        await using var echo = new Support.EchoTcpServer(echoPort);

        var proxy = new TcpProxyConfig
        {
            Name = "tcp-tls",
            Type = ProtocolType.Tcp,
            LocalPort = (ushort)echoPort,
            RemotePort = (ushort)Support.TestPorts.Allocate()
        };

        await using var harness = new Support.ProxyTestHarness(
            Support.ProxyTestHarness.CreateServerConfig(controlPort, tlsEnabled: true),
            Support.ProxyTestHarness.CreateClientConfig(controlPort, tlsEnabled: true, encryption: TransportEncryptionMethod.None, proxy));

        await harness.WaitReadyAsync();
        var response = await Support.TcpTestClient.SendAsync(proxy.RemotePort, "tls-payload");
        Assert.Equal("tls-payload", response);
    }

    [Fact]
    public async Task TcpProxy_Works_WithTlsAndAes128Cfb()
    {
        var controlPort = Support.TestPorts.Allocate();
        var echoPort = Support.TestPorts.Allocate();
        await using var echo = new Support.EchoTcpServer(echoPort);

        var proxy = new TcpProxyConfig
        {
            Name = "tcp-layered",
            Type = ProtocolType.Tcp,
            LocalPort = (ushort)echoPort,
            RemotePort = (ushort)Support.TestPorts.Allocate()
        };

        await using var harness = new Support.ProxyTestHarness(
            Support.ProxyTestHarness.CreateServerConfig(controlPort, null, null, null, tlsEnabled: true, encryption: TransportEncryptionMethod.Aes128Cfb),
            Support.ProxyTestHarness.CreateClientConfig(controlPort, tlsEnabled: true, encryption: TransportEncryptionMethod.Aes128Cfb, proxy));

        await harness.WaitReadyAsync();
        var response = await Support.TcpTestClient.SendAsync(proxy.RemotePort, "layered-payload");
        Assert.Equal("layered-payload", response);
    }
}
