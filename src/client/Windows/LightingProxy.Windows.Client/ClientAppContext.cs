using System.Text.Json;
using LightingProxy.Domain.Configuration;
using LightingProxy.Domain.Enums;
using LightingProxy.Windows.Shared.Models;
using LightingProxy.Windows.Shared.Services;
using LightingProxy.Windows.Shared.ViewModels;

namespace LightingProxy.Windows.Client;

public sealed class ClientAppContext
{
    public ClientHostService HostService { get; } = new();

    public ConfigurationAppService ConfigurationService { get; } = new();

    public AppPreferences Preferences { get; private set; } = AppPreferences.CreateDefault("client");

    public ClientDashboardViewModel DashboardViewModel { get; private set; } = null!;

    public ConfigurationViewModel ConfigurationViewModel { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Preferences = await AppPreferencesStore.LoadAsync("client").ConfigureAwait(false);
        DashboardViewModel = new ClientDashboardViewModel(HostService);
        ConfigurationViewModel = new ConfigurationViewModel(
            ConfigurationKind.Client,
            Preferences,
            ApplyConfigurationAsync);
    }

    public async Task SavePreferencesAsync()
    {
        ConfigurationViewModel.ApplyPreferences(Preferences);
        Preferences = ConfigurationViewModel.ToPreferences();
        await AppPreferencesStore.SaveAsync("client", Preferences).ConfigureAwait(false);
    }

    public async Task StartTunnelAsync()
    {
        var options = Preferences.ToSourceOptions(ConfigurationKind.Client);
        var config = await ConfigurationService.LoadClientAsync(options).ConfigureAwait(false)
                     ?? ConfigurationService.CreateDefaultClientConfig();
        await HostService.StartAsync(config).ConfigureAwait(false);
        await DashboardViewModel.RefreshAsync().ConfigureAwait(false);
    }

    public async Task StopTunnelAsync()
    {
        await HostService.StopAsync().ConfigureAwait(false);
        await DashboardViewModel.RefreshAsync().ConfigureAwait(false);
    }

    private async Task ApplyConfigurationAsync()
    {
        await SavePreferencesAsync().ConfigureAwait(false);
        await StopTunnelAsync().ConfigureAwait(false);
        await StartTunnelAsync().ConfigureAwait(false);
    }
}
