using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Client.Proxies;

public class StcpProxyConfig : ProxyConfigBase
{
    public StcpProxyConfig()
    {
        Type = ProtocolType.Stcp;
    }

    public required string SecretKey { get; set; }

    public IList<string>? AllowUsers { get; set; }
}
