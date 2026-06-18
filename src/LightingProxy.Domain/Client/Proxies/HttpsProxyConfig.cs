using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Client.Proxies;

public class HttpsProxyConfig : ProxyConfigBase
{
    public HttpsProxyConfig()
    {
        Type = ProtocolType.Https;
    }

    public IList<string>? CustomDomains { get; set; }

    public string? SubDomain { get; set; }

    public string? HttpUser { get; set; }

    public string? HttpPassword { get; set; }

    public IList<string>? Locations { get; set; }

    public string? HostHeaderRewrite { get; set; }
}
