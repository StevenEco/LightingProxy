using LightingProxy.Domain.Client;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Persistence;
using LightingProxy.Domain.Server;

namespace LightingProxy.Extension.Configuration;

public sealed class JsonFileConfigurationStore : IConfigurationStore
{
    private readonly string _filePath;
    private readonly ConfigurationKind _kind;
    private readonly bool _validate;

    public JsonFileConfigurationStore(string filePath, ConfigurationKind kind, bool validate = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _kind = kind;
        _validate = validate;
    }

    public Task<ServerConfig?> GetServerConfigAsync(string name = "default", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_kind != ConfigurationKind.Server || !File.Exists(_filePath))
        {
            return Task.FromResult<ServerConfig?>(null);
        }

        var json = File.ReadAllText(_filePath);
        return Task.FromResult<ServerConfig?>(ConfigurationSerializer.DeserializeServerConfig(json, _validate, _filePath));
    }

    public Task SaveServerConfigAsync(ServerConfig config, string name = "default", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(config);

        if (_kind != ConfigurationKind.Server)
        {
            throw new InvalidOperationException("This configuration store is not bound to server configuration.");
        }

        EnsureDirectory(_filePath);
        File.WriteAllText(_filePath, ConfigurationSerializer.SerializeServerConfig(config, _validate));
        return Task.CompletedTask;
    }

    public Task<ClientConfig?> GetClientConfigAsync(string name = "default", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_kind != ConfigurationKind.Client || !File.Exists(_filePath))
        {
            return Task.FromResult<ClientConfig?>(null);
        }

        var json = File.ReadAllText(_filePath);
        return Task.FromResult<ClientConfig?>(ConfigurationSerializer.DeserializeClientConfig(json, _validate, _filePath));
    }

    public Task SaveClientConfigAsync(ClientConfig config, string name = "default", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(config);

        if (_kind != ConfigurationKind.Client)
        {
            throw new InvalidOperationException("This configuration store is not bound to client configuration.");
        }

        EnsureDirectory(_filePath);
        File.WriteAllText(_filePath, ConfigurationSerializer.SerializeClientConfig(config, _validate));
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(ConfigurationKind kind, string name = "default", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_kind == kind && File.Exists(_filePath));
    }

    public Task DeleteAsync(ConfigurationKind kind, string name = "default", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_kind == kind && File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }

        return Task.CompletedTask;
    }

    private static void EnsureDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
