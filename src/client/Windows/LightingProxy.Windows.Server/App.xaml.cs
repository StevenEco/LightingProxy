using Microsoft.UI.Xaml;

namespace LightingProxy.Windows.Server;

public partial class App : Application
{
    private Window? _window;

    public static Window? MainWindow => ((App)Current)._window;

    public ServerAppContext Context { get; } = new();

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await Context.InitializeAsync();
        _window = new MainWindow();
        _window.Activate();
        try
        {
            await Context.StartServerAsync();
        }
        catch
        {
            // 首次启动可能尚无有效配置。
        }
    }
}
