using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Client.Proxies;

public class KcpProxyConfig : ProxyConfigBase
{
    public KcpProxyConfig()
    {
        Type = ProtocolType.Kcp;
    }

    public ushort RemotePort { get; set; }

    public string? RemoteAddress { get; set; }
}
