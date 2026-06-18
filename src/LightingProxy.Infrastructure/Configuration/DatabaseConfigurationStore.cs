using LightingProxy.Domain.Client;
using LightingProxy.Domain.Configuration;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Persistence;
using LightingProxy.Domain.Server;
using LightingProxy.Extension.Configuration;
using LightingProxy.Infrastructure.Persistence;
using LightingProxy.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LightingProxy.Infrastructure.Configuration;

public sealed class DatabaseConfigurationStore : IConfigurationStore, IAsyncDisposable, IDisposable
{
    private readonly LightingProxyDbContext _dbContext;
    private readonly bool _validate;
    private readonly bool _ownsDbContext;

    public DatabaseConfigurationStore(DatabaseConfigurationOptions options, bool validate = true)
        : this(DbContextFactory.Create(options), validate, ownsDbContext: true)
    {
        if (options.AutoCreateDatabase)
        {
            _dbContext.Database.EnsureCreated();
        }
    }

    public DatabaseConfigurationStore(LightingProxyDbContext dbContext, bool validate = true, bool ownsDbContext = false)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _validate = validate;
        _ownsDbContext = ownsDbContext;
    }

    public async Task<ServerConfig?> GetServerConfigAsync(string name = "default", CancellationToken cancellationToken = default)
    {
        var record = await FindRecordAsync(ConfigurationKind.Server, name, cancellationToken).ConfigureAwait(false);
        return record is null ? null : ConfigurationSerializer.DeserializeServerConfig(record.Content, _validate, $"database:{name}:server");
    }

    public async Task SaveServerConfigAsync(ServerConfig config, string name = "default", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        var content = ConfigurationSerializer.SerializeServerConfig(config, _validate);
        await UpsertAsync(ConfigurationKind.Server, name, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClientConfig?> GetClientConfigAsync(string name = "default", CancellationToken cancellationToken = default)
    {
        var record = await FindRecordAsync(ConfigurationKind.Client, name, cancellationToken).ConfigureAwait(false);
        return record is null ? null : ConfigurationSerializer.DeserializeClientConfig(record.Content, _validate, $"database:{name}:client");
    }

    public async Task SaveClientConfigAsync(ClientConfig config, string name = "default", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        var content = ConfigurationSerializer.SerializeClientConfig(config, _validate);
        await UpsertAsync(ConfigurationKind.Client, name, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(ConfigurationKind kind, string name = "default", CancellationToken cancellationToken = default)
    {
        return await _dbContext.ConfigurationRecords
            .AnyAsync(record => record.Name == name && record.Kind == kind, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(ConfigurationKind kind, string name = "default", CancellationToken cancellationToken = default)
    {
        var record = await FindRecordAsync(kind, name, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return;
        }

        _dbContext.ConfigurationRecords.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_ownsDbContext)
        {
            _dbContext.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsDbContext)
        {
            return _dbContext.DisposeAsync();
        }

        return ValueTask.CompletedTask;
    }

    private async Task<ConfigurationRecord?> FindRecordAsync(
        ConfigurationKind kind,
        string name,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ConfigurationRecords
            .FirstOrDefaultAsync(record => record.Name == name && record.Kind == kind, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task UpsertAsync(
        ConfigurationKind kind,
        string name,
        string content,
        CancellationToken cancellationToken)
    {
        var record = await FindRecordAsync(kind, name, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        if (record is null)
        {
            _dbContext.ConfigurationRecords.Add(new ConfigurationRecord
            {
                Name = name,
                Kind = kind,
                Content = content,
                UpdatedAt = now
            });
        }
        else
        {
            record.Content = content;
            record.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
