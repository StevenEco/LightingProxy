using LightingProxy.Windows.Client.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LightingProxy.Windows.Client;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow()
    {
        InitializeComponent();
        Title = "LightingProxy 客户端";

        _refreshTimer.Tick += async (_, _) => await AppContext.DashboardViewModel.RefreshAsync();
        _refreshTimer.Start();

        ContentFrame.Navigate(typeof(DashboardPage));
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    public static ClientAppContext AppContext => ((App)Application.Current).Context;

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        var page = item.Tag?.ToString() switch
        {
            "config" => typeof(ConfigurationPage),
            _ => typeof(DashboardPage)
        };

        ContentFrame.Navigate(page);
    }
}
