using LightingProxy.Core.Proxies;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Core.Tests;

public class ProxyHandlerRegistryTests
{
    [Theory]
    [InlineData(ProtocolType.Tcp)]
    [InlineData(ProtocolType.Udp)]
    [InlineData(ProtocolType.Http)]
    [InlineData(ProtocolType.Https)]
    [InlineData(ProtocolType.Stcp)]
    [InlineData(ProtocolType.Xtcp)]
    [InlineData(ProtocolType.Socks5)]
    [InlineData(ProtocolType.Tcpmux)]
    [InlineData(ProtocolType.Kcp)]
    public void CreateDefault_RegistersAllProtocols(ProtocolType protocol)
    {
        var registry = ProxyHandlerRegistry.CreateDefault();
        var handler = registry.Get(protocol);
        Assert.Equal(protocol, handler.Protocol);
    }

    [Fact]
    public void BuildMetadata_IncludesRemotePortForTcp()
    {
        var handler = new TcpProxyHandler();
        var metadata = handler.BuildMetadata(new TcpProxyConfig
        {
            Name = "tcp",
            LocalAddress = "127.0.0.1",
            LocalPort = 8080,
            RemotePort = 6000
        });

        Assert.Equal("6000", metadata["remotePort"]);
    }
}
