using LightingProxy.Domain.Common;

namespace LightingProxy.Domain.Transport;

public class ServerTransportConfig
{
    public int MaxPoolCount { get; set; } = 5;

    public int TcpKeepAliveSeconds { get; set; } = 7200;

    public bool TcpMux { get; set; } = true;

    public int TcpMuxKeepaliveIntervalSeconds { get; set; } = 30;

    public TlsConfig? Tls { get; set; }
}
