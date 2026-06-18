namespace LightingProxy.Domain.Server;

public class VhostConfig
{
    public ushort HttpPort { get; set; }

    public ushort HttpsPort { get; set; }

    public ushort TcpmuxHttpConnectPort { get; set; }
}
