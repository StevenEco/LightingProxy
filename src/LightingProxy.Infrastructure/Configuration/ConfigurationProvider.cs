using LightingProxy.Domain.Configuration;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Persistence;
using LightingProxy.Extension.Configuration;

namespace LightingProxy.Infrastructure.Configuration;

public static class ConfigurationProvider
{
    public static IConfigurationStore CreateStore(ConfigurationSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Storage switch
        {
            ConfigurationStorageKind.File => FileConfigurationStoreFactory.Create(options),
            ConfigurationStorageKind.Database => CreateDatabaseStore(options),
            _ => throw new NotSupportedException($"Unsupported storage kind: {options.Storage}")
        };
    }

    private static IConfigurationStore CreateDatabaseStore(ConfigurationSourceOptions options)
    {
        if (options.Database is null)
        {
            throw new InvalidOperationException("Database options are required when storage kind is Database.");
        }

        return new DatabaseConfigurationStore(options.Database, options.Validate);
    }
}
