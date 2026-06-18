namespace LightingProxy.Core.Protocol;

public enum MessageType
{
    Login,
    LoginResp,
    NewProxy,
    NewProxyResp,
    NewVisitor,
    NewVisitorResp,
    ReqWorkConn,
    StartWorkConn,
    WorkConnReady,
    WorkConnStarted,
    UdpPacket,
    Ping,
    Pong,
    OpenVisitorTunnel
}
