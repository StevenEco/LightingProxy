namespace LightingProxy.Domain.Common;

/// <summary>
/// frpc 本地 Web 管理界面配置。
/// </summary>
public class WebHostConfig
{
    public string Address { get; set; } = "127.0.0.1";

    public ushort Port { get; set; } = 7400;

    public string? User { get; set; }

    public string? Password { get; set; }

    public TlsConfig? Tls { get; set; }
}
