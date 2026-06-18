using LightingProxy.Domain.Common;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Transport;

public class ClientTransportConfig
{
    public TransportProtocol Protocol { get; set; } = TransportProtocol.Tcp;

    public int PoolCount { get; set; } = 1;

    public int HeartbeatIntervalSeconds { get; set; } = 30;

    public int HeartbeatTimeoutSeconds { get; set; } = 90;

    public TlsConfig Tls { get; set; } = new();

    public TransportEncryptionConfig Encryption { get; set; } = new();
}
