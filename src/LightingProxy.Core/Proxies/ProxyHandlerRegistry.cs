using System.Collections.Concurrent;
using LightingProxy.Core.Abstractions;
using LightingProxy.Core.Protocol;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Core.Proxies;

public static class ProxyMetadataBuilder
{
    public static Dictionary<string, string> Build(ProxyConfigBase config)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["localAddress"] = config.LocalAddress,
            ["localPort"] = config.LocalPort.ToString(),
            ["enabled"] = config.Enabled.ToString()
        };

        switch (config)
        {
            case TcpProxyConfig tcp:
                metadata["remotePort"] = tcp.RemotePort.ToString();
                if (tcp.RemoteAddress is not null) metadata["remoteAddress"] = tcp.RemoteAddress;
                break;
            case UdpProxyConfig udp:
                metadata["remotePort"] = udp.RemotePort.ToString();
                if (udp.RemoteAddress is not null) metadata["remoteAddress"] = udp.RemoteAddress;
                break;
            case HttpProxyConfig http:
                if (http.CustomDomains is not null) metadata["customDomains"] = string.Join(',', http.CustomDomains);
                if (http.SubDomain is not null) metadata["subDomain"] = http.SubDomain;
                if (http.HostHeaderRewrite is not null) metadata["hostHeaderRewrite"] = http.HostHeaderRewrite;
                break;
            case HttpsProxyConfig https:
                if (https.CustomDomains is not null) metadata["customDomains"] = string.Join(',', https.CustomDomains);
                if (https.SubDomain is not null) metadata["subDomain"] = https.SubDomain;
                break;
            case StcpProxyConfig stcp:
                metadata["secretKey"] = stcp.SecretKey;
                break;
            case XtcpProxyConfig xtcp:
                metadata["secretKey"] = xtcp.SecretKey;
                break;
            case Socks5ProxyConfig socks5:
                if (socks5.Username is not null) metadata["username"] = socks5.Username;
                if (socks5.Password is not null) metadata["password"] = socks5.Password;
                break;
            case TcpmuxProxyConfig tcpmux:
                if (tcpmux.CustomDomains is not null) metadata["customDomains"] = string.Join(',', tcpmux.CustomDomains);
                if (tcpmux.Multiplexer is not null) metadata["multiplexer"] = tcpmux.Multiplexer;
                break;
            case KcpProxyConfig kcp:
                metadata["remotePort"] = kcp.RemotePort.ToString();
                if (kcp.RemoteAddress is not null) metadata["remoteAddress"] = kcp.RemoteAddress;
                break;
        }

        return metadata;
    }

    public static ushort GetRemotePort(IReadOnlyDictionary<string, string> metadata)
        => ushort.Parse(metadata["remotePort"]);
}

public sealed class ProxyHandlerRegistry
{
    private readonly Dictionary<ProtocolType, IProxyHandler> _handlers = new();

    public ProxyHandlerRegistry Register(IProxyHandler handler)
    {
        _handlers[handler.Protocol] = handler;
        return this;
    }

    public IProxyHandler Get(ProtocolType protocol)
        => _handlers.TryGetValue(protocol, out var handler)
            ? handler
            : throw new NotSupportedException($"Protocol '{protocol}' is not supported.");

    public static ProxyHandlerRegistry CreateDefault()
        => new ProxyHandlerRegistry()
            .Register(new TcpProxyHandler())
            .Register(new UdpProxyHandler())
            .Register(new HttpProxyHandler())
            .Register(new HttpsProxyHandler())
            .Register(new StcpProxyHandler())
            .Register(new XtcpProxyHandler())
            .Register(new Socks5ProxyHandler())
            .Register(new TcpmuxProxyHandler())
            .Register(new KcpProxyHandler());
}
