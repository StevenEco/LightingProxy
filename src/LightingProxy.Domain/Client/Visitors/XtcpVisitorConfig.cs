using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Client.Visitors;

public class XtcpVisitorConfig : VisitorConfigBase
{
    public XtcpVisitorConfig()
    {
        Type = ProtocolType.Xtcp;
    }

    public bool KeepTunnelOpen { get; set; }
}
