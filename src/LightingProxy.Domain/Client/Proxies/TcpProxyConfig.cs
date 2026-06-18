using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Client.Proxies;

public class TcpProxyConfig : ProxyConfigBase
{
    public TcpProxyConfig()
    {
        Type = ProtocolType.Tcp;
    }

    public ushort RemotePort { get; set; }

    public string? RemoteAddress { get; set; }
}
