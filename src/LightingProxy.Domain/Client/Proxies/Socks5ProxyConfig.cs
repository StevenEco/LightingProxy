using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Client.Proxies;

public class Socks5ProxyConfig : ProxyConfigBase
{
    public Socks5ProxyConfig()
    {
        Type = ProtocolType.Socks5;
    }

    public string? Username { get; set; }

    public string? Password { get; set; }
}
