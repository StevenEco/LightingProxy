using LightingProxy.Domain.Enums;
using LightingProxy.Windows.Client;
using LightingProxy.Windows.Shared.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace LightingProxy.Windows.Client.Pages;

public sealed partial class ConfigurationPage : Page
{
    public ConfigurationViewModel ViewModel => MainWindow.AppContext.ConfigurationViewModel;

    public ConfigurationPage()
    {
        InitializeComponent();
        Loaded += (_, _) => SyncUiFromViewModel();
    }

    private void SyncUiFromViewModel()
    {
        StorageBox.SelectedIndex = ViewModel.Storage == ConfigurationStorageKind.Database ? 1 : 0;
        UpdatePanels();
        FileFormatBox.SelectedIndex = ViewModel.FileFormat == ConfigurationFileFormat.Ini ? 1 : 0;
        DatabaseProviderBox.SelectedIndex = ViewModel.DatabaseProvider switch
        {
            DatabaseProvider.MySql => 0,
            DatabaseProvider.SqlServer => 1,
            DatabaseProvider.Sqlite => 2,
            DatabaseProvider.PostgreSql => 3,
            _ => 2
        };
    }

    private void StorageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.Storage = StorageBox.SelectedIndex == 1
            ? ConfigurationStorageKind.Database
            : ConfigurationStorageKind.File;
        UpdatePanels();
    }

    private void UpdatePanels()
    {
        var isDatabase = ViewModel.Storage == ConfigurationStorageKind.Database;
        FilePanel.Visibility = isDatabase ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        DatabasePanel.Visibility = isDatabase ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    private async void BrowseFile_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var mainWindow = App.MainWindow;
        if (mainWindow is null)
        {
            return;
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".json");
        picker.FileTypeFilter.Add(".ini");
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            ViewModel.FilePath = file.Path;
        }
    }

    private void ApplyUiToViewModel()
    {
        ViewModel.FileFormat = FileFormatBox.SelectedIndex == 1
            ? ConfigurationFileFormat.Ini
            : ConfigurationFileFormat.Json;
        ViewModel.DatabaseProvider = DatabaseProviderBox.SelectedIndex switch
        {
            0 => DatabaseProvider.MySql,
            1 => DatabaseProvider.SqlServer,
            3 => DatabaseProvider.PostgreSql,
            _ => DatabaseProvider.Sqlite
        };
    }

    private async void Load_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ApplyUiToViewModel();
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void Save_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ApplyUiToViewModel();
        await ViewModel.SaveCommand.ExecuteAsync(null);
        await MainWindow.AppContext.SavePreferencesAsync();
    }

    private async void Apply_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ApplyUiToViewModel();
        await ViewModel.ApplyCommand.ExecuteAsync(null);
    }
}
