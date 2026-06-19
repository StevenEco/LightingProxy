using LightingProxy.Domain.Configuration;
using LightingProxy.Domain.Enums;
using LightingProxy.Windows.Shared.Models;
using LightingProxy.Windows.Shared.Services;
using LightingProxy.Windows.Shared.ViewModels;

namespace LightingProxy.Windows.Server;

public sealed class ServerAppContext
{
    public ServerHostService HostService { get; } = new();

    public ConfigurationAppService ConfigurationService { get; } = new();

    public AppPreferences Preferences { get; private set; } = AppPreferences.CreateDefault("server");

    public ServerDashboardViewModel DashboardViewModel { get; private set; } = null!;

    public ConfigurationViewModel ConfigurationViewModel { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Preferences = await AppPreferencesStore.LoadAsync("server").ConfigureAwait(false);
        DashboardViewModel = new ServerDashboardViewModel(HostService);
        ConfigurationViewModel = new ConfigurationViewModel(
            ConfigurationKind.Server,
            Preferences,
            ApplyConfigurationAsync);
    }

    public async Task SavePreferencesAsync()
    {
        ConfigurationViewModel.ApplyPreferences(Preferences);
        Preferences = ConfigurationViewModel.ToPreferences();
        await AppPreferencesStore.SaveAsync("server", Preferences).ConfigureAwait(false);
    }

    public async Task StartServerAsync()
    {
        var options = Preferences.ToSourceOptions(ConfigurationKind.Server);
        var config = await ConfigurationService.LoadServerAsync(options).ConfigureAwait(false)
                     ?? ConfigurationService.CreateDefaultServerConfig();
        await HostService.StartAsync(config).ConfigureAwait(false);
        await DashboardViewModel.RefreshAsync().ConfigureAwait(false);
    }

    public async Task StopServerAsync()
    {
        await HostService.StopAsync().ConfigureAwait(false);
        await DashboardViewModel.RefreshAsync().ConfigureAwait(false);
    }

    private async Task ApplyConfigurationAsync()
    {
        await SavePreferencesAsync().ConfigureAwait(false);
        await StopServerAsync().ConfigureAwait(false);
        await StartServerAsync().ConfigureAwait(false);
    }
}
