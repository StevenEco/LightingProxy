using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Client.Proxies;

public class TcpmuxProxyConfig : ProxyConfigBase
{
    public TcpmuxProxyConfig()
    {
        Type = ProtocolType.Tcpmux;
    }

    public IList<string>? CustomDomains { get; set; }

    public string? RouteByHttpUser { get; set; }

    public string? Multiplexer { get; set; }
}
