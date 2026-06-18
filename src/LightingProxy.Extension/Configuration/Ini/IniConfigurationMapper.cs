using LightingProxy.Domain.Client;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Client.Visitors;
using LightingProxy.Domain.Common;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Server;
using LightingProxy.Domain.Transport;

namespace LightingProxy.Extension.Configuration.Ini;

internal static class IniValueReader
{
    public static string? Get(IDictionary<string, string> section, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (section.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    public static string GetRequired(IDictionary<string, string> section, params string[] keys)
    {
        return Get(section, keys) ?? throw new InvalidOperationException($"Missing required key: {keys[0]}");
    }

    public static ushort GetUInt16(IDictionary<string, string> section, ushort defaultValue, params string[] keys)
    {
        var value = Get(section, keys);
        return value is null ? defaultValue : ushort.Parse(value);
    }

    public static int GetInt32(IDictionary<string, string> section, int defaultValue, params string[] keys)
    {
        var value = Get(section, keys);
        return value is null ? defaultValue : int.Parse(value);
    }

    public static bool GetBoolean(IDictionary<string, string> section, bool defaultValue, params string[] keys)
    {
        var value = Get(section, keys);
        return value is null ? defaultValue : bool.Parse(value);
    }

    public static TEnum GetEnum<TEnum>(IDictionary<string, string> section, TEnum defaultValue, params string[] keys)
        where TEnum : struct, Enum
    {
        var value = Get(section, keys);
        return value is null ? defaultValue : Enum.Parse<TEnum>(value, ignoreCase: true);
    }

    public static IList<string>? GetList(IDictionary<string, string> section, params string[] keys)
    {
        var value = Get(section, keys);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}

internal static class IniConfigurationMapper
{
    private static readonly HashSet<string> ReservedClientSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "common", "auth", "transport", "log", "web_host", "webhost"
    };

    private static readonly HashSet<string> ReservedServerSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "common", "auth", "transport", "log", "web_server", "webserver", "vhost"
    };

    public static ServerConfig ParseServer(string content, string? source = null)
    {
        var sections = IniDocument.Parse(content);
        sections.TryGetValue("common", out var common);
        common ??= new Dictionary<string, string>();

        sections.TryGetValue("auth", out var auth);
        auth ??= new Dictionary<string, string>();

        sections.TryGetValue("transport", out var transport);
        transport ??= new Dictionary<string, string>();

        sections.TryGetValue("log", out var log);
        log ??= new Dictionary<string, string>();

        sections.TryGetValue("web_server", out var webServer);
        webServer ??= sections.GetValueOrDefault("webserver") ?? new Dictionary<string, string>();

        sections.TryGetValue("vhost", out var vhost);
        vhost ??= new Dictionary<string, string>();

        var config = new ServerConfig
        {
            BindAddress = IniValueReader.Get(common, "bind_addr", "bindAddress") ?? "0.0.0.0",
            BindPort = IniValueReader.GetUInt16(common, 7000, "bind_port", "bindPort"),
            ProxyBindAddress = IniValueReader.Get(common, "proxy_bind_addr", "proxyBindAddress"),
            KcpBindPort = IniValueReader.GetUInt16(common, 0, "kcp_bind_port", "kcpBindPort"),
            QuicBindPort = IniValueReader.GetUInt16(common, 0, "quic_bind_port", "quicBindPort"),
            Auth = new AuthConfig
            {
                Method = IniValueReader.GetEnum(auth, AuthMethod.Token, "method"),
                Token = IniValueReader.Get(auth, "token") ?? IniValueReader.Get(common, "token")
            },
            Transport = new ServerTransportConfig
            {
                MaxPoolCount = IniValueReader.GetInt32(transport, 5, "max_pool_count", "maxPoolCount"),
                TcpKeepAliveSeconds = IniValueReader.GetInt32(transport, 7200, "tcp_keep_alive", "tcpKeepAliveSeconds"),
                TcpMux = IniValueReader.GetBoolean(transport, true, "tcp_mux", "tcpMux"),
                TcpMuxKeepaliveIntervalSeconds = IniValueReader.GetInt32(transport, 30, "tcp_mux_keepalive_interval", "tcpMuxKeepaliveIntervalSeconds"),
                Tls = ParseTlsConfig(transport),
                Encryption = ParseEncryptionConfig(transport)
            },
            Log = new LogConfig
            {
                Target = IniValueReader.GetEnum(log, LogTarget.Console, "target", "to"),
                FilePath = IniValueReader.Get(log, "file", "filePath"),
                Level = IniValueReader.GetEnum(log, LogLevel.Info, "level"),
                MaxDays = IniValueReader.GetInt32(log, 3, "max_days", "maxDays")
            }
        };

        if (IniValueReader.Get(webServer, "address", "addr") is not null || IniValueReader.GetUInt16(webServer, 0, "port") > 0)
        {
            config.WebServer = new WebServerConfig
            {
                Address = IniValueReader.Get(webServer, "address", "addr") ?? "127.0.0.1",
                Port = IniValueReader.GetUInt16(webServer, 7500, "port"),
                User = IniValueReader.Get(webServer, "user"),
                Password = IniValueReader.Get(webServer, "password")
            };
        }

        if (HasAny(vhost, "http_port", "httpPort", "https_port", "httpsPort", "tcpmux_httpconnect_port", "tcpmuxHttpConnectPort"))
        {
            config.Vhost = new VhostConfig
            {
                HttpPort = IniValueReader.GetUInt16(vhost, 0, "http_port", "httpPort"),
                HttpsPort = IniValueReader.GetUInt16(vhost, 0, "https_port", "httpsPort"),
                TcpmuxHttpConnectPort = IniValueReader.GetUInt16(vhost, 0, "tcpmux_httpconnect_port", "tcpmuxHttpConnectPort")
            };
        }

        return config;
    }

    public static ClientConfig ParseClient(string content, string? source = null)
    {
        var sections = IniDocument.Parse(content);
        sections.TryGetValue("common", out var common);
        common ??= new Dictionary<string, string>();

        sections.TryGetValue("auth", out var auth);
        auth ??= new Dictionary<string, string>();

        sections.TryGetValue("transport", out var transport);
        transport ??= new Dictionary<string, string>();

        sections.TryGetValue("log", out var log);
        log ??= new Dictionary<string, string>();

        sections.TryGetValue("web_host", out var webHost);
        webHost ??= sections.GetValueOrDefault("webhost") ?? new Dictionary<string, string>();

        var config = new ClientConfig
        {
            ServerAddress = IniValueReader.GetRequired(common, "server_addr", "serverAddress"),
            ServerPort = IniValueReader.GetUInt16(common, 7000, "server_port", "serverPort"),
            User = IniValueReader.Get(common, "user"),
            DnsServer = IniValueReader.Get(common, "dns_server", "dnsServer"),
            Auth = new AuthConfig
            {
                Method = IniValueReader.GetEnum(auth, AuthMethod.Token, "method"),
                Token = IniValueReader.Get(auth, "token") ?? IniValueReader.Get(common, "token")
            },
            Transport = new ClientTransportConfig
            {
                Protocol = IniValueReader.GetEnum(transport, TransportProtocol.Tcp, "protocol"),
                PoolCount = IniValueReader.GetInt32(transport, 1, "pool_count", "poolCount"),
                HeartbeatIntervalSeconds = IniValueReader.GetInt32(transport, 30, "heartbeat_interval", "heartbeatIntervalSeconds"),
                HeartbeatTimeoutSeconds = IniValueReader.GetInt32(transport, 90, "heartbeat_timeout", "heartbeatTimeoutSeconds"),
                Tls = ParseTlsConfig(transport),
                Encryption = ParseEncryptionConfig(transport)
            },
            Log = new LogConfig
            {
                Target = IniValueReader.GetEnum(log, LogTarget.Console, "target", "to"),
                FilePath = IniValueReader.Get(log, "file", "filePath"),
                Level = IniValueReader.GetEnum(log, LogLevel.Info, "level"),
                MaxDays = IniValueReader.GetInt32(log, 3, "max_days", "maxDays")
            },
            Proxies = [],
            Visitors = []
        };

        if (IniValueReader.Get(webHost, "address", "addr") is not null || IniValueReader.GetUInt16(webHost, 0, "port") > 0)
        {
            config.WebHost = new WebHostConfig
            {
                Address = IniValueReader.Get(webHost, "address", "addr") ?? "127.0.0.1",
                Port = IniValueReader.GetUInt16(webHost, 7400, "port"),
                User = IniValueReader.Get(webHost, "user"),
                Password = IniValueReader.Get(webHost, "password")
            };
        }

        foreach (var (sectionName, section) in sections)
        {
            if (ReservedClientSections.Contains(sectionName))
            {
                continue;
            }

            var role = IniValueReader.Get(section, "role");
            var type = IniValueReader.Get(section, "type") ?? "tcp";

            if (string.Equals(role, "visitor", StringComparison.OrdinalIgnoreCase))
            {
                config.Visitors!.Add(ParseVisitor(sectionName, section, type));
                continue;
            }

            config.Proxies!.Add(ParseProxy(sectionName, section, type));
        }

        return config;
    }

    public static string SerializeServer(ServerConfig config)
    {
        var sections = new Dictionary<string, Dictionary<string, string?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["common"] = new()
            {
                ["bind_addr"] = config.BindAddress,
                ["bind_port"] = config.BindPort.ToString(),
                ["proxy_bind_addr"] = config.ProxyBindAddress,
                ["kcp_bind_port"] = ToStringOrNull(config.KcpBindPort),
                ["quic_bind_port"] = ToStringOrNull(config.QuicBindPort)
            },
            ["auth"] = new()
            {
                ["method"] = config.Auth.Method.ToString().ToLowerInvariant(),
                ["token"] = config.Auth.Token
            },
            ["transport"] = new()
            {
                ["max_pool_count"] = config.Transport.MaxPoolCount.ToString(),
                ["tcp_keep_alive"] = config.Transport.TcpKeepAliveSeconds.ToString(),
                ["tcp_mux"] = config.Transport.TcpMux.ToString().ToLowerInvariant(),
                ["tcp_mux_keepalive_interval"] = config.Transport.TcpMuxKeepaliveIntervalSeconds.ToString()
            },
            ["log"] = new()
            {
                ["target"] = config.Log.Target.ToString().ToLowerInvariant(),
                ["file"] = config.Log.FilePath,
                ["level"] = config.Log.Level.ToString().ToLowerInvariant(),
                ["max_days"] = config.Log.MaxDays.ToString()
            }
        };

        if (config.WebServer is not null)
        {
            sections["web_server"] = new()
            {
                ["address"] = config.WebServer.Address,
                ["port"] = config.WebServer.Port.ToString(),
                ["user"] = config.WebServer.User,
                ["password"] = config.WebServer.Password
            };
        }

        if (config.Vhost is not null)
        {
            sections["vhost"] = new()
            {
                ["http_port"] = ToStringOrNull(config.Vhost.HttpPort),
                ["https_port"] = ToStringOrNull(config.Vhost.HttpsPort),
                ["tcpmux_httpconnect_port"] = ToStringOrNull(config.Vhost.TcpmuxHttpConnectPort)
            };
        }

        return IniDocument.Serialize(sections);
    }

    public static string SerializeClient(ClientConfig config)
    {
        var sections = new Dictionary<string, Dictionary<string, string?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["common"] = new()
            {
                ["server_addr"] = config.ServerAddress,
                ["server_port"] = config.ServerPort.ToString(),
                ["user"] = config.User,
                ["dns_server"] = config.DnsServer
            },
            ["auth"] = new()
            {
                ["method"] = config.Auth.Method.ToString().ToLowerInvariant(),
                ["token"] = config.Auth.Token
            },
            ["transport"] = new()
            {
                ["protocol"] = config.Transport.Protocol.ToString().ToLowerInvariant(),
                ["pool_count"] = config.Transport.PoolCount.ToString(),
                ["heartbeat_interval"] = config.Transport.HeartbeatIntervalSeconds.ToString(),
                ["heartbeat_timeout"] = config.Transport.HeartbeatTimeoutSeconds.ToString()
            },
            ["log"] = new()
            {
                ["target"] = config.Log.Target.ToString().ToLowerInvariant(),
                ["file"] = config.Log.FilePath,
                ["level"] = config.Log.Level.ToString().ToLowerInvariant(),
                ["max_days"] = config.Log.MaxDays.ToString()
            }
        };

        if (config.WebHost is not null)
        {
            sections["web_host"] = new()
            {
                ["address"] = config.WebHost.Address,
                ["port"] = config.WebHost.Port.ToString(),
                ["user"] = config.WebHost.User,
                ["password"] = config.WebHost.Password
            };
        }

        if (config.Proxies is not null)
        {
            foreach (var proxy in config.Proxies)
            {
                sections[proxy.Name] = SerializeProxy(proxy);
            }
        }

        if (config.Visitors is not null)
        {
            foreach (var visitor in config.Visitors)
            {
                sections[visitor.Name] = SerializeVisitor(visitor);
            }
        }

        return IniDocument.Serialize(sections);
    }

    private static ProxyConfigBase ParseProxy(string sectionName, IDictionary<string, string> section, string type)
    {
        var name = IniValueReader.Get(section, "name") ?? sectionName;
        var localAddress = IniValueReader.Get(section, "local_ip", "localAddress") ?? "127.0.0.1";
        var localPort = IniValueReader.GetUInt16(section, 0, "local_port", "localPort");
        var enabled = IniValueReader.GetBoolean(section, true, "enabled");

        ProxyConfigBase proxy = type.ToLowerInvariant() switch
        {
            "udp" => new UdpProxyConfig
            {
                Name = name,
                RemotePort = IniValueReader.GetUInt16(section, 0, "remote_port", "remotePort"),
                RemoteAddress = IniValueReader.Get(section, "remote_ip", "remoteAddress")
            },
            "http" => new HttpProxyConfig
            {
                Name = name,
                CustomDomains = IniValueReader.GetList(section, "custom_domains", "customDomains"),
                SubDomain = IniValueReader.Get(section, "subdomain", "subDomain"),
                HttpUser = IniValueReader.Get(section, "http_user", "httpUser"),
                HttpPassword = IniValueReader.Get(section, "http_password", "httpPassword"),
                Locations = IniValueReader.GetList(section, "locations"),
                HostHeaderRewrite = IniValueReader.Get(section, "host_header_rewrite", "hostHeaderRewrite")
            },
            "https" => new HttpsProxyConfig
            {
                Name = name,
                CustomDomains = IniValueReader.GetList(section, "custom_domains", "customDomains"),
                SubDomain = IniValueReader.Get(section, "subdomain", "subDomain"),
                HttpUser = IniValueReader.Get(section, "http_user", "httpUser"),
                HttpPassword = IniValueReader.Get(section, "http_password", "httpPassword"),
                Locations = IniValueReader.GetList(section, "locations"),
                HostHeaderRewrite = IniValueReader.Get(section, "host_header_rewrite", "hostHeaderRewrite")
            },
            "stcp" => new StcpProxyConfig
            {
                Name = name,
                SecretKey = IniValueReader.GetRequired(section, "secret_key", "secretKey")
            },
            "xtcp" => new XtcpProxyConfig
            {
                Name = name,
                SecretKey = IniValueReader.GetRequired(section, "secret_key", "secretKey")
            },
            "socks5" => new Socks5ProxyConfig
            {
                Name = name,
                Username = IniValueReader.Get(section, "username", "user"),
                Password = IniValueReader.Get(section, "password")
            },
            "tcpmux" => new TcpmuxProxyConfig
            {
                Name = name,
                CustomDomains = IniValueReader.GetList(section, "custom_domains", "customDomains"),
                RouteByHttpUser = IniValueReader.Get(section, "route_by_http_user", "routeByHttpUser"),
                Multiplexer = IniValueReader.Get(section, "multiplexer")
            },
            _ => new TcpProxyConfig
            {
                Name = name,
                RemotePort = IniValueReader.GetUInt16(section, 0, "remote_port", "remotePort"),
                RemoteAddress = IniValueReader.Get(section, "remote_ip", "remoteAddress")
            }
        };

        proxy.LocalAddress = localAddress;
        proxy.LocalPort = localPort;
        proxy.Enabled = enabled;
        return proxy;
    }

    private static VisitorConfigBase ParseVisitor(string sectionName, IDictionary<string, string> section, string type)
    {
        var name = IniValueReader.Get(section, "name") ?? sectionName;
        var serverName = IniValueReader.GetRequired(section, "server_name", "serverName");
        var secretKey = IniValueReader.GetRequired(section, "secret_key", "secretKey");
        var bindAddress = IniValueReader.Get(section, "bind_addr", "bindAddress") ?? "127.0.0.1";
        var bindPort = IniValueReader.GetUInt16(section, 0, "bind_port", "bindPort");
        var enabled = IniValueReader.GetBoolean(section, true, "enabled");

        VisitorConfigBase visitor = type.ToLowerInvariant() switch
        {
            "xtcp" => new XtcpVisitorConfig
            {
                Name = name,
                ServerName = serverName,
                SecretKey = secretKey,
                KeepTunnelOpen = IniValueReader.GetBoolean(section, false, "keep_tunnel_open", "keepTunnelOpen")
            },
            _ => new StcpVisitorConfig
            {
                Name = name,
                ServerName = serverName,
                SecretKey = secretKey
            }
        };

        visitor.BindAddress = bindAddress;
        visitor.BindPort = bindPort;
        visitor.Enabled = enabled;
        return visitor;
    }

    private static Dictionary<string, string?> SerializeProxy(ProxyConfigBase proxy)
    {
        var section = new Dictionary<string, string?>
        {
            ["type"] = proxy.Type.ToString().ToLowerInvariant(),
            ["local_ip"] = proxy.LocalAddress,
            ["local_port"] = proxy.LocalPort.ToString(),
            ["enabled"] = proxy.Enabled.ToString().ToLowerInvariant()
        };

        switch (proxy)
        {
            case TcpProxyConfig tcp:
                section["remote_port"] = tcp.RemotePort.ToString();
                section["remote_ip"] = tcp.RemoteAddress;
                break;
            case UdpProxyConfig udp:
                section["remote_port"] = udp.RemotePort.ToString();
                section["remote_ip"] = udp.RemoteAddress;
                break;
            case HttpProxyConfig http:
                section["custom_domains"] = JoinList(http.CustomDomains);
                section["subdomain"] = http.SubDomain;
                section["http_user"] = http.HttpUser;
                section["http_password"] = http.HttpPassword;
                section["locations"] = JoinList(http.Locations);
                section["host_header_rewrite"] = http.HostHeaderRewrite;
                break;
            case HttpsProxyConfig https:
                section["custom_domains"] = JoinList(https.CustomDomains);
                section["subdomain"] = https.SubDomain;
                section["http_user"] = https.HttpUser;
                section["http_password"] = https.HttpPassword;
                section["locations"] = JoinList(https.Locations);
                section["host_header_rewrite"] = https.HostHeaderRewrite;
                break;
            case StcpProxyConfig stcp:
                section["secret_key"] = stcp.SecretKey;
                section["allow_users"] = JoinList(stcp.AllowUsers);
                break;
            case XtcpProxyConfig xtcp:
                section["secret_key"] = xtcp.SecretKey;
                section["allow_users"] = JoinList(xtcp.AllowUsers);
                break;
            case Socks5ProxyConfig socks5:
                section["username"] = socks5.Username;
                section["password"] = socks5.Password;
                break;
            case TcpmuxProxyConfig tcpmux:
                section["custom_domains"] = JoinList(tcpmux.CustomDomains);
                section["route_by_http_user"] = tcpmux.RouteByHttpUser;
                section["multiplexer"] = tcpmux.Multiplexer;
                break;
        }

        return section;
    }

    private static Dictionary<string, string?> SerializeVisitor(VisitorConfigBase visitor)
    {
        var section = new Dictionary<string, string?>
        {
            ["role"] = "visitor",
            ["type"] = visitor.Type.ToString().ToLowerInvariant(),
            ["server_name"] = visitor.ServerName,
            ["secret_key"] = visitor.SecretKey,
            ["bind_addr"] = visitor.BindAddress,
            ["bind_port"] = visitor.BindPort.ToString(),
            ["enabled"] = visitor.Enabled.ToString().ToLowerInvariant()
        };

        if (visitor is XtcpVisitorConfig xtcp)
        {
            section["keep_tunnel_open"] = xtcp.KeepTunnelOpen.ToString().ToLowerInvariant();
        }

        return section;
    }

    private static bool HasAny(IDictionary<string, string> section, params string[] keys)
    {
        return keys.Any(key => section.ContainsKey(key) && !string.IsNullOrWhiteSpace(section[key]));
    }

    private static TlsConfig ParseTlsConfig(IDictionary<string, string> transport)
        => new()
        {
            Enabled = IniValueReader.GetBoolean(transport, false, "tls_enable", "tlsEnable", "tls.enable"),
            Force = IniValueReader.GetBoolean(transport, false, "tls_force", "tlsForce", "tls.force"),
            CertFile = IniValueReader.Get(transport, "tls_cert_file", "tlsCertFile", "tls.certFile"),
            KeyFile = IniValueReader.Get(transport, "tls_key_file", "tlsKeyFile", "tls.keyFile"),
            TrustedCaFile = IniValueReader.Get(transport, "tls_trusted_ca_file", "tlsTrustedCaFile", "tls.trustedCaFile"),
            ServerName = IniValueReader.Get(transport, "tls_server_name", "tlsServerName", "tls.serverName")
        };

    private static TransportEncryptionConfig ParseEncryptionConfig(IDictionary<string, string> transport)
    {
        var useEncryption = IniValueReader.GetBoolean(transport, false, "use_encryption", "useEncryption", "transport.useEncryption");
        var method = IniValueReader.GetEnum(
            transport,
            useEncryption ? TransportEncryptionMethod.Aes128Cfb : TransportEncryptionMethod.None,
            "encryption", "encryption_method", "encryptionMethod", "transport.encryption");

        return new TransportEncryptionConfig { Method = method };
    }

    private static string? ToStringOrNull(ushort value) => value == 0 ? null : value.ToString();

    private static string? JoinList(IList<string>? values)
        => values is null || values.Count == 0 ? null : string.Join(',', values);
}
