using LightingProxy.Domain.Client;
using LightingProxy.Domain.Configuration;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Persistence;
using LightingProxy.Domain.Server;
using LightingProxy.Extension.Configuration;

namespace LightingProxy.Extension.Utils;

public static class ConfigurationHelper
{
    public static ServerConfig LoadServerConfig(string filePath, bool validate = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var json = File.ReadAllText(filePath);
        return ConfigurationSerializer.DeserializeServerConfig(json, validate, filePath);
    }

    public static ServerConfig LoadServerConfig(Stream stream, bool validate = true)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream, leaveOpen: true);
        var json = reader.ReadToEnd();
        return ConfigurationSerializer.DeserializeServerConfig(json, validate);
    }

    public static async Task<ServerConfig> LoadServerConfigAsync(string filePath, bool validate = true, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        return ConfigurationSerializer.DeserializeServerConfig(json, validate, filePath);
    }

    public static ClientConfig LoadClientConfig(string filePath, bool validate = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var json = File.ReadAllText(filePath);
        return ConfigurationSerializer.DeserializeClientConfig(json, validate, filePath);
    }

    public static ClientConfig LoadClientConfig(Stream stream, bool validate = true)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream, leaveOpen: true);
        var json = reader.ReadToEnd();
        return ConfigurationSerializer.DeserializeClientConfig(json, validate);
    }

    public static async Task<ClientConfig> LoadClientConfigAsync(string filePath, bool validate = true, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        return ConfigurationSerializer.DeserializeClientConfig(json, validate, filePath);
    }

    public static void SaveServerConfig(string filePath, ServerConfig config, bool validate = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(config);

        EnsureDirectory(filePath);
        File.WriteAllText(filePath, ConfigurationSerializer.SerializeServerConfig(config, validate));
    }

    public static void SaveClientConfig(string filePath, ClientConfig config, bool validate = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(config);

        EnsureDirectory(filePath);
        File.WriteAllText(filePath, ConfigurationSerializer.SerializeClientConfig(config, validate));
    }

    public static async Task<ServerConfig?> LoadServerConfigAsync(
        ConfigurationSourceOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var store = CreateStore(options);
        return await store.GetServerConfigAsync(options.Name, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<ClientConfig?> LoadClientConfigAsync(
        ConfigurationSourceOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var store = CreateStore(options);
        return await store.GetClientConfigAsync(options.Name, cancellationToken).ConfigureAwait(false);
    }

    public static IConfigurationStore CreateStore(ConfigurationSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Storage != ConfigurationStorageKind.File)
        {
            throw new InvalidOperationException("Use LightingProxy.Infrastructure.Configuration.ConfigurationProvider for database storage.");
        }

        return FileConfigurationStoreFactory.Create(options);
    }

    public static ConfigurationWatcher<ServerConfig> WatchServerConfig(
        string filePath,
        Action<ServerConfig> onChanged,
        bool validate = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(onChanged);

        return new ConfigurationWatcher<ServerConfig>(
            filePath,
            path => LoadServerConfig(path, validate),
            onChanged);
    }

    public static ConfigurationWatcher<ClientConfig> WatchClientConfig(
        string filePath,
        Action<ClientConfig> onChanged,
        bool validate = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(onChanged);

        return new ConfigurationWatcher<ClientConfig>(
            filePath,
            path => LoadClientConfig(path, validate),
            onChanged);
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
