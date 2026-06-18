using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Client.Proxies;

public class UdpProxyConfig : ProxyConfigBase
{
    public UdpProxyConfig()
    {
        Type = ProtocolType.Udp;
    }

    public ushort RemotePort { get; set; }

    public string? RemoteAddress { get; set; }
}
