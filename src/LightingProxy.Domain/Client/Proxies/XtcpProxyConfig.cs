using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Client.Proxies;

public class XtcpProxyConfig : ProxyConfigBase
{
    public XtcpProxyConfig()
    {
        Type = ProtocolType.Xtcp;
    }

    public required string SecretKey { get; set; }

    public IList<string>? AllowUsers { get; set; }
}
