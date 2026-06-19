using LightingProxy.Domain.Configuration;
using LightingProxy.Domain.Enums;
using LightingProxy.Windows.Shared.Models;

namespace LightingProxy.CLI.Shared;

public sealed class CliConfigurationContext
{
    public CliConfigurationContext(string appName, ConfigurationKind kind)
    {
        AppName = appName;
        Kind = kind;
    }

    public string AppName { get; }

    public ConfigurationKind Kind { get; }

    public AppPreferences BuildPreferences(CliOptions options)
    {
        var preferences = AppPreferences.CreateDefault(AppName);

        if (!string.IsNullOrWhiteSpace(options.ConfigPath))
        {
            preferences.Storage = ConfigurationStorageKind.File;
            preferences.FilePath = Path.GetFullPath(options.ConfigPath);
            preferences.FileFormat = options.FileFormat ?? InferFileFormat(preferences.FilePath);
        }

        if (options.Storage is not null)
        {
            preferences.Storage = options.Storage.Value;
        }

        if (options.FileFormat is not null)
        {
            preferences.FileFormat = options.FileFormat.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.ConfigName))
        {
            preferences.ConfigName = options.ConfigName;
        }

        if (options.DatabaseProvider is not null)
        {
            preferences.DatabaseProvider = options.DatabaseProvider.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            preferences.ConnectionString = options.ConnectionString;
        }

        if (options.AutoCreateDatabase is not null)
        {
            preferences.AutoCreateDatabase = options.AutoCreateDatabase.Value;
        }

        preferences.ValidateOnSave = options.ValidateOnSave;
        return preferences;
    }

    public ConfigurationSourceOptions ToSourceOptions(AppPreferences preferences)
        => preferences.ToSourceOptions(Kind);

    private static ConfigurationFileFormat InferFileFormat(string filePath)
        => Path.GetExtension(filePath).Equals(".ini", StringComparison.OrdinalIgnoreCase)
            ? ConfigurationFileFormat.Ini
            : ConfigurationFileFormat.Json;
}
