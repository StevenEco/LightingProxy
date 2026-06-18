namespace LightingProxy.Domain.Common;

/// <summary>
/// frps 管理面板（Dashboard）配置。
/// </summary>
public class WebServerConfig
{
    public string Address { get; set; } = "127.0.0.1";

    public ushort Port { get; set; } = 7500;

    public string? User { get; set; }

    public string? Password { get; set; }

    public TlsConfig? Tls { get; set; }
}
