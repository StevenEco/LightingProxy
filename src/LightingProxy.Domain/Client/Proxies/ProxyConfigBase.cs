using System.Text.Json.Serialization;
using LightingProxy.Domain.Common;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Client.Proxies;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TcpProxyConfig), "tcp")]
[JsonDerivedType(typeof(UdpProxyConfig), "udp")]
[JsonDerivedType(typeof(HttpProxyConfig), "http")]
[JsonDerivedType(typeof(HttpsProxyConfig), "https")]
[JsonDerivedType(typeof(StcpProxyConfig), "stcp")]
[JsonDerivedType(typeof(XtcpProxyConfig), "xtcp")]
[JsonDerivedType(typeof(Socks5ProxyConfig), "socks5")]
[JsonDerivedType(typeof(TcpmuxProxyConfig), "tcpmux")]
[JsonDerivedType(typeof(KcpProxyConfig), "kcp")]
public abstract class ProxyConfigBase
{
    public required string Name { get; set; }

    [JsonIgnore]
    public ProtocolType Type { get; set; }

    public string LocalAddress { get; set; } = "127.0.0.1";

    public ushort LocalPort { get; set; }

    public bool Enabled { get; set; } = true;

    public BandwidthConfig? Bandwidth { get; set; }

    public HealthCheckConfig? HealthCheck { get; set; }
}
