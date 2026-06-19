using LightingProxy.Domain.Client;
using LightingProxy.Domain.Configuration;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Persistence;
using LightingProxy.Domain.Server;
using LightingProxy.Extension.Utils;
using LightingProxy.Infrastructure.Configuration;

namespace LightingProxy.Windows.Shared.Services;

public sealed class ConfigurationAppService
{
    public async Task<ClientConfig?> LoadClientAsync(ConfigurationSourceOptions options, CancellationToken cancellationToken = default)
    {
        var store = CreateStore(options);
        await using var disposable = store as IAsyncDisposable;
        return await store.GetClientConfigAsync(options.Name, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ServerConfig?> LoadServerAsync(ConfigurationSourceOptions options, CancellationToken cancellationToken = default)
    {
        var store = CreateStore(options);
        await using var disposable = store as IAsyncDisposable;
        return await store.GetServerConfigAsync(options.Name, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveClientAsync(ClientConfig config, ConfigurationSourceOptions options, CancellationToken cancellationToken = default)
    {
        var store = CreateStore(options);
        await using var disposable = store as IAsyncDisposable;
        await store.SaveClientConfigAsync(config, options.Name, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveServerAsync(ServerConfig config, ConfigurationSourceOptions options, CancellationToken cancellationToken = default)
    {
        var store = CreateStore(options);
        await using var disposable = store as IAsyncDisposable;
        await store.SaveServerConfigAsync(config, options.Name, cancellationToken).ConfigureAwait(false);
    }

    public ClientConfig CreateDefaultClientConfig()
        => new()
        {
            ServerAddress = "127.0.0.1",
            ServerPort = 7000,
            Auth = new Domain.Common.AuthConfig { Method = AuthMethod.Token, Token = "change-me" },
            Proxies = []
        };

    public ServerConfig CreateDefaultServerConfig()
        => new()
        {
            BindAddress = "0.0.0.0",
            BindPort = 7000,
            Auth = new Domain.Common.AuthConfig { Method = AuthMethod.Token, Token = "change-me" }
        };

    private static IConfigurationStore CreateStore(ConfigurationSourceOptions options)
        => options.Storage == ConfigurationStorageKind.Database
            ? ConfigurationProvider.CreateStore(options)
            : ConfigurationHelper.CreateStore(options);
}
