using LightingProxy.Windows.Client;
using LightingProxy.Windows.Shared.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace LightingProxy.Windows.Client.Pages;

public sealed partial class DashboardPage : Page
{
    public ClientDashboardViewModel ViewModel => MainWindow.AppContext.DashboardViewModel;

    public DashboardPage()
    {
        InitializeComponent();
    }

    private async void Start_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await MainWindow.AppContext.StartTunnelAsync();

    private async void Stop_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await MainWindow.AppContext.StopTunnelAsync();
}
