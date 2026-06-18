using LightingProxy.Domain.Client;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Client.Visitors;
using LightingProxy.Domain.Common;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Server;
using LightingProxy.Domain.Transport;

namespace LightingProxy.Extension.Validation;

public static class ConfigurationValidator
{
    public static IReadOnlyList<string> Validate(ServerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<string>();

        if (config.BindPort == 0)
        {
            errors.Add("Server bindPort must be greater than 0.");
        }

        ValidateAuth(config.Auth, "server", errors);
        ValidateLog(config.Log, "server", errors);

        if (config.Transport.MaxPoolCount <= 0)
        {
            errors.Add("Server transport.maxPoolCount must be greater than 0.");
        }

        if (config.WebServer is not null)
        {
            ValidateWebEndpoint(config.WebServer.Address, config.WebServer.Port, "server.webServer", errors);
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(ClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.ServerAddress))
        {
            errors.Add("Client serverAddress is required.");
        }

        if (config.ServerPort == 0)
        {
            errors.Add("Client serverPort must be greater than 0.");
        }

        ValidateAuth(config.Auth, "client", errors);
        ValidateLog(config.Log, "client", errors);
        ValidateTransport(config.Transport, errors);

        if (config.WebHost is not null)
        {
            ValidateWebEndpoint(config.WebHost.Address, config.WebHost.Port, "client.webHost", errors);
        }

        ValidateProxies(config.Proxies, errors);
        ValidateVisitors(config.Visitors, errors);

        return errors;
    }

    private static void ValidateAuth(AuthConfig auth, string scope, List<string> errors)
    {
        switch (auth.Method)
        {
            case AuthMethod.Token when string.IsNullOrWhiteSpace(auth.Token):
                errors.Add($"{scope} auth.token is required when auth.method is token.");
                break;
            case AuthMethod.Oidc when string.IsNullOrWhiteSpace(auth.Oidc?.Issuer):
                errors.Add($"{scope} auth.oidc.issuer is required when auth.method is oidc.");
                break;
        }
    }

    private static void ValidateLog(LogConfig log, string scope, List<string> errors)
    {
        if (log.Target == LogTarget.File && string.IsNullOrWhiteSpace(log.FilePath))
        {
            errors.Add($"{scope} log.filePath is required when log.target is file.");
        }
    }

    private static void ValidateTransport(ClientTransportConfig transport, List<string> errors)
    {
        if (transport.PoolCount <= 0)
        {
            errors.Add("Client transport.poolCount must be greater than 0.");
        }

        if (transport.HeartbeatIntervalSeconds <= 0)
        {
            errors.Add("Client transport.heartbeatIntervalSeconds must be greater than 0.");
        }

        if (transport.HeartbeatTimeoutSeconds <= 0)
        {
            errors.Add("Client transport.heartbeatTimeoutSeconds must be greater than 0.");
        }

        if (transport.HeartbeatTimeoutSeconds <= transport.HeartbeatIntervalSeconds)
        {
            errors.Add("Client transport.heartbeatTimeoutSeconds must be greater than heartbeatIntervalSeconds.");
        }
    }

    private static void ValidateWebEndpoint(string address, ushort port, string scope, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            errors.Add($"{scope}.address is required.");
        }

        if (port == 0)
        {
            errors.Add($"{scope}.port must be greater than 0.");
        }
    }

    private static void ValidateProxies(IList<ProxyConfigBase>? proxies, List<string> errors)
    {
        if (proxies is null || proxies.Count == 0)
        {
            return;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < proxies.Count; i++)
        {
            var proxy = proxies[i];
            var path = $"proxies[{i}]";

            if (string.IsNullOrWhiteSpace(proxy.Name))
            {
                errors.Add($"{path}.name is required.");
                continue;
            }

            if (!names.Add(proxy.Name))
            {
                errors.Add($"{path}.name '{proxy.Name}' is duplicated.");
            }

            if (!proxy.Enabled)
            {
                continue;
            }

            if (proxy.LocalPort == 0)
            {
                errors.Add($"{path}.localPort must be greater than 0.");
            }

            switch (proxy)
            {
                case TcpProxyConfig tcp when tcp.RemotePort == 0:
                    errors.Add($"{path}.remotePort must be greater than 0 for tcp proxy.");
                    break;
                case UdpProxyConfig udp when udp.RemotePort == 0:
                    errors.Add($"{path}.remotePort must be greater than 0 for udp proxy.");
                    break;
                case HttpProxyConfig http when !HasHttpRoute(http.CustomDomains, http.SubDomain):
                    errors.Add($"{path} requires customDomains or subDomain for http proxy.");
                    break;
                case HttpsProxyConfig https when !HasHttpRoute(https.CustomDomains, https.SubDomain):
                    errors.Add($"{path} requires customDomains or subDomain for https proxy.");
                    break;
                case StcpProxyConfig stcp when string.IsNullOrWhiteSpace(stcp.SecretKey):
                    errors.Add($"{path}.secretKey is required for stcp proxy.");
                    break;
                case XtcpProxyConfig xtcp when string.IsNullOrWhiteSpace(xtcp.SecretKey):
                    errors.Add($"{path}.secretKey is required for xtcp proxy.");
                    break;
                case TcpmuxProxyConfig tcpmux when string.IsNullOrWhiteSpace(tcpmux.Multiplexer):
                    errors.Add($"{path}.multiplexer is required for tcpmux proxy.");
                    break;
                case TcpmuxProxyConfig tcpmux when !HasHttpRoute(tcpmux.CustomDomains, null):
                    errors.Add($"{path} requires customDomains for tcpmux proxy.");
                    break;
            }
        }
    }

    private static void ValidateVisitors(IList<VisitorConfigBase>? visitors, List<string> errors)
    {
        if (visitors is null || visitors.Count == 0)
        {
            return;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < visitors.Count; i++)
        {
            var visitor = visitors[i];
            var path = $"visitors[{i}]";

            if (string.IsNullOrWhiteSpace(visitor.Name))
            {
                errors.Add($"{path}.name is required.");
                continue;
            }

            if (!names.Add(visitor.Name))
            {
                errors.Add($"{path}.name '{visitor.Name}' is duplicated.");
            }

            if (!visitor.Enabled)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(visitor.ServerName))
            {
                errors.Add($"{path}.serverName is required.");
            }

            if (string.IsNullOrWhiteSpace(visitor.SecretKey))
            {
                errors.Add($"{path}.secretKey is required.");
            }

            if (visitor.BindPort == 0)
            {
                errors.Add($"{path}.bindPort must be greater than 0.");
            }
        }
    }

    private static bool HasHttpRoute(IList<string>? customDomains, string? subDomain)
    {
        if (!string.IsNullOrWhiteSpace(subDomain))
        {
            return true;
        }

        return customDomains is not null && customDomains.Any(domain => !string.IsNullOrWhiteSpace(domain));
    }
}
