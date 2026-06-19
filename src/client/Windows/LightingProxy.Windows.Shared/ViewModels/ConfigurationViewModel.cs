using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightingProxy.Domain.Configuration;
using LightingProxy.Domain.Enums;
using LightingProxy.Windows.Shared.Helpers;
using LightingProxy.Windows.Shared.Models;
using LightingProxy.Windows.Shared.Services;

namespace LightingProxy.Windows.Shared.ViewModels;

public partial class ConfigurationViewModel : ObservableObject
{
    private readonly ConfigurationAppService _configurationService = new();
    private readonly ConfigurationKind _kind;
    private readonly Func<Task> _onApplied;

    public ConfigurationViewModel(ConfigurationKind kind, AppPreferences preferences, Func<Task> onApplied)
    {
        _kind = kind;
        _onApplied = onApplied;
        ApplyPreferences(preferences);
    }

    [ObservableProperty]
    private ConfigurationStorageKind _storage = ConfigurationStorageKind.File;

    [ObservableProperty]
    private ConfigurationFileFormat _fileFormat = ConfigurationFileFormat.Json;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private DatabaseProvider _databaseProvider = DatabaseProvider.Sqlite;

    [ObservableProperty]
    private string _connectionString = string.Empty;

    [ObservableProperty]
    private bool _autoCreateDatabase = true;

    [ObservableProperty]
    private string _configName = "default";

    [ObservableProperty]
    private string _configJson = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public bool IsFileStorage => Storage == ConfigurationStorageKind.File;

    public bool IsDatabaseStorage => Storage == ConfigurationStorageKind.Database;

    partial void OnStorageChanged(ConfigurationStorageKind value)
    {
        OnPropertyChanged(nameof(IsFileStorage));
        OnPropertyChanged(nameof(IsDatabaseStorage));
    }

    public AppPreferences ToPreferences()
        => new()
        {
            Storage = Storage,
            FileFormat = FileFormat,
            FilePath = FilePath,
            DatabaseProvider = DatabaseProvider,
            ConnectionString = ConnectionString,
            AutoCreateDatabase = AutoCreateDatabase,
            ConfigName = ConfigName
        };

    public void ApplyPreferences(AppPreferences preferences)
    {
        Storage = preferences.Storage;
        FileFormat = preferences.FileFormat;
        FilePath = preferences.FilePath;
        DatabaseProvider = preferences.DatabaseProvider;
        ConnectionString = preferences.ConnectionString;
        AutoCreateDatabase = preferences.AutoCreateDatabase;
        ConfigName = preferences.ConfigName;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var options = ToPreferences().ToSourceOptions(_kind);
            if (_kind == ConfigurationKind.Client)
            {
                var config = await _configurationService.LoadClientAsync(options).ConfigureAwait(false)
                             ?? _configurationService.CreateDefaultClientConfig();
                ConfigJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                var config = await _configurationService.LoadServerAsync(options).ConfigureAwait(false)
                             ?? _configurationService.CreateDefaultServerConfig();
                ConfigJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            }

            StatusMessage = "配置已加载。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var options = ToPreferences().ToSourceOptions(_kind);
            if (_kind == ConfigurationKind.Client)
            {
                var config = JsonSerializer.Deserialize<Domain.Client.ClientConfig>(ConfigJson)
                             ?? throw new InvalidOperationException("客户端配置 JSON 无效。");
                await _configurationService.SaveClientAsync(config, options).ConfigureAwait(false);
            }
            else
            {
                var config = JsonSerializer.Deserialize<Domain.Server.ServerConfig>(ConfigJson)
                             ?? throw new InvalidOperationException("服务端配置 JSON 无效。");
                await _configurationService.SaveServerAsync(config, options).ConfigureAwait(false);
            }

            StatusMessage = "配置已保存。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        await SaveAsync().ConfigureAwait(false);
        if (StatusMessage.StartsWith("保存失败", StringComparison.Ordinal))
        {
            return;
        }

        await _onApplied().ConfigureAwait(false);
        StatusMessage = "配置已应用。";
    }
}
