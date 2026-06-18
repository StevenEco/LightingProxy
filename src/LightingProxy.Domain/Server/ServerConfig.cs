using LightingProxy.Domain.Common;
using LightingProxy.Domain.Transport;

namespace LightingProxy.Domain.Server;

public class ServerConfig
{
    public string BindAddress { get; set; } = "0.0.0.0";

    public ushort BindPort { get; set; } = 7000;

    public string? ProxyBindAddress { get; set; }

    public ushort KcpBindPort { get; set; }

    public ushort QuicBindPort { get; set; }

    public VhostConfig? Vhost { get; set; }

    public AuthConfig Auth { get; set; } = new();

    public ServerTransportConfig Transport { get; set; } = new();

    public WebServerConfig? WebServer { get; set; }

    public LogConfig Log { get; set; } = new();
}
