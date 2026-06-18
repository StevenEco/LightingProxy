using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Client.Visitors;

public class StcpVisitorConfig : VisitorConfigBase
{
    public StcpVisitorConfig()
    {
        Type = ProtocolType.Stcp;
    }
}
