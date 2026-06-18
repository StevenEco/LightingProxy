using System.Text.Json.Serialization;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Client.Visitors;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(StcpVisitorConfig), "stcp")]
[JsonDerivedType(typeof(XtcpVisitorConfig), "xtcp")]
public abstract class VisitorConfigBase
{
    public required string Name { get; set; }

    [JsonIgnore]
    public ProtocolType Type { get; set; }

    /// <summary>
    /// 要访问的服务端代理名称。
    /// </summary>
    public required string ServerName { get; set; }

    public required string SecretKey { get; set; }

    public string BindAddress { get; set; } = "127.0.0.1";

    public ushort BindPort { get; set; }

    public bool Enabled { get; set; } = true;
}
