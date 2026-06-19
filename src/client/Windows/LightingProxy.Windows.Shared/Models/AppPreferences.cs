using System.Text.Json;
using LightingProxy.Domain.Configuration;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Windows.Shared.Models;

public sealed class AppPreferences
{
    public ConfigurationStorageKind Storage { get; set; } = ConfigurationStorageKind.File;

    public ConfigurationFileFormat FileFormat { get; set; } = ConfigurationFileFormat.Json;

    public string FilePath { get; set; } = string.Empty;

    public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.Sqlite;

    public string ConnectionString { get; set; } = "Data Source=lightingproxy.db";

    public bool AutoCreateDatabase { get; set; } = true;

    public string ConfigName { get; set; } = "default";

    public bool ValidateOnSave { get; set; } = true;

    public static AppPreferences CreateDefault(string appName)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LightingProxy",
            appName);
        Directory.CreateDirectory(folder);

        return new AppPreferences
        {
            FilePath = Path.Combine(folder, "config.json"),
            ConnectionString = $"Data Source={Path.Combine(folder, "config.db")}"
        };
    }

    public ConfigurationSourceOptions ToSourceOptions(ConfigurationKind kind)
        => new()
        {
            Storage = Storage,
            FileFormat = FileFormat,
            FilePath = FilePath,
            Name = ConfigName,
            TargetKind = kind,
            Validate = ValidateOnSave,
            Database = Storage == ConfigurationStorageKind.Database
                ? new DatabaseConfigurationOptions
                {
                    Provider = DatabaseProvider,
                    ConnectionString = ConnectionString,
                    AutoCreateDatabase = AutoCreateDatabase
                }
                : null
        };
}

public static class AppPreferencesStore
{
    public static async Task<AppPreferences> LoadAsync(string appName)
    {
        var path = GetPath(appName);
        if (!File.Exists(path))
        {
            return AppPreferences.CreateDefault(appName);
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AppPreferences>(stream).ConfigureAwait(false)
               ?? AppPreferences.CreateDefault(appName);
    }

    public static async Task SaveAsync(string appName, AppPreferences preferences)
    {
        var path = GetPath(appName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, preferences, new JsonSerializerOptions { WriteIndented = true }).ConfigureAwait(false);
    }

    private static string GetPath(string appName)
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LightingProxy",
            appName,
            "ui-preferences.json");
}
