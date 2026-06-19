using LightingProxy.Windows.Server;
using LightingProxy.Windows.Shared.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace LightingProxy.Windows.Server.Pages;

public sealed partial class DashboardPage : Page
{
    public ServerDashboardViewModel ViewModel => MainWindow.AppContext.DashboardViewModel;

    public DashboardPage()
    {
        InitializeComponent();
    }

    private async void Stop_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await MainWindow.AppContext.StopServerAsync();
}
