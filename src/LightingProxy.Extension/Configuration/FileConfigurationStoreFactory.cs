using LightingProxy.Domain.Configuration;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Persistence;

namespace LightingProxy.Extension.Configuration;

public static class FileConfigurationStoreFactory
{
    public static IConfigurationStore Create(ConfigurationSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Storage != ConfigurationStorageKind.File)
        {
            throw new InvalidOperationException("Configuration storage kind must be File.");
        }

        if (string.IsNullOrWhiteSpace(options.FilePath))
        {
            throw new InvalidOperationException("Configuration file path is required.");
        }

        if (options.TargetKind is null)
        {
            throw new InvalidOperationException("Configuration target kind is required for file storage.");
        }

        return options.FileFormat switch
        {
            ConfigurationFileFormat.Json => new JsonFileConfigurationStore(options.FilePath, options.TargetKind.Value, options.Validate),
            ConfigurationFileFormat.Ini => new IniFileConfigurationStore(options.FilePath, options.TargetKind.Value, options.Validate),
            _ => throw new NotSupportedException($"Unsupported file format: {options.FileFormat}")
        };
    }
}
