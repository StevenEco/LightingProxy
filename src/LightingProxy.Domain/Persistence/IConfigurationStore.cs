using LightingProxy.Domain.Client;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Server;

namespace LightingProxy.Domain.Persistence;

public interface IConfigurationStore
{
    Task<ServerConfig?> GetServerConfigAsync(string name = "default", CancellationToken cancellationToken = default);

    Task SaveServerConfigAsync(ServerConfig config, string name = "default", CancellationToken cancellationToken = default);

    Task<ClientConfig?> GetClientConfigAsync(string name = "default", CancellationToken cancellationToken = default);

    Task SaveClientConfigAsync(ClientConfig config, string name = "default", CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(ConfigurationKind kind, string name = "default", CancellationToken cancellationToken = default);

    Task DeleteAsync(ConfigurationKind kind, string name = "default", CancellationToken cancellationToken = default);
}
