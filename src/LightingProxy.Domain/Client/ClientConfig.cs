using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Client.Visitors;
using LightingProxy.Domain.Common;
using LightingProxy.Domain.Transport;

namespace LightingProxy.Domain.Client;

public class ClientConfig
{
    public required string ServerAddress { get; set; }

    public ushort ServerPort { get; set; } = 7000;

    /// <summary>
    /// 多用户模式下的用户名，需与服务端配置一致。
    /// </summary>
    public string? User { get; set; }

    public string? DnsServer { get; set; }

    public AuthConfig Auth { get; set; } = new();

    public ClientTransportConfig Transport { get; set; } = new();

    public WebHostConfig? WebHost { get; set; }

    public LogConfig Log { get; set; } = new();

    public IList<ProxyConfigBase>? Proxies { get; set; }

    public IList<VisitorConfigBase>? Visitors { get; set; }
}
