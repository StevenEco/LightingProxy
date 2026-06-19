using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightingProxy.Core.Telemetry;
using LightingProxy.Windows.Shared.Helpers;
using LightingProxy.Windows.Shared.Services;

namespace LightingProxy.Windows.Shared.ViewModels;

public partial class TunnelTrafficItem : ObservableObject
{
    public TunnelTrafficItem(string name, string detail)
    {
        Name = name;
        Detail = detail;
    }

    public string Name { get; }

    public string Detail { get; }

    [ObservableProperty]
    private string _trafficSummary = "0 B";
}

public partial class ClientDashboardViewModel : ObservableObject
{
    private readonly ClientHostService _hostService;

    public ClientDashboardViewModel(ClientHostService hostService)
    {
        _hostService = hostService;
        _hostService.StateChanged += (_, _) => _ = RefreshAsync();
    }

    [ObservableProperty]
    private string _statusTitle = "未连接";

    [ObservableProperty]
    private string _statusSubtitle = "等待启动隧道客户端";

    [ObservableProperty]
    private string _uptimeText = "--";

    [ObservableProperty]
    private string _inboundSpeedText = "0 B/s";

    [ObservableProperty]
    private string _outboundSpeedText = "0 B/s";

    [ObservableProperty]
    private string _totalTrafficText = "0 B";

    [ObservableProperty]
    private string _heartbeatText = "--";

    public ObservableCollection<TunnelTrafficItem> Tunnels { get; } = [];

    public bool IsRunning => _hostService.ConnectionState == ProxyConnectionState.Connected
                             || _hostService.ConnectionState == ProxyConnectionState.Starting;

    [RelayCommand]
    private async Task StartAsync()
    {
        OnPropertyChanged(nameof(IsRunning));
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        await _hostService.StopAsync().ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    public async Task RefreshAsync()
    {
        StatusTitle = _hostService.ConnectionState switch
        {
            ProxyConnectionState.Connected => "隧道在线",
            ProxyConnectionState.Starting => "正在连接",
            ProxyConnectionState.Faulted => "连接异常",
            _ => "未连接"
        };

        StatusSubtitle = _hostService.ConnectionState switch
        {
            ProxyConnectionState.Connected => "控制通道稳定，代理转发可用",
            ProxyConnectionState.Starting => "正在与远端建立控制连接",
            ProxyConnectionState.Faulted => _hostService.LastError ?? "请检查服务端地址与认证配置",
            _ => "在配置页应用设置后点击启动"
        };

        UptimeText = _hostService.ConnectedAt is null
            ? "--"
            : TrafficFormat.Duration(DateTimeOffset.UtcNow - _hostService.ConnectedAt.Value);

        HeartbeatText = _hostService.LastHeartbeatUtc?.ToLocalTime().ToString("HH:mm:ss") ?? "--";

        var runtime = _hostService.Runtime;
        if (runtime is not null)
        {
            var snapshot = runtime.GetTotalSnapshot();
            InboundSpeedText = TrafficFormat.Speed(snapshot.BytesIn > 0 ? snapshot.BytesPerSecond / 2 : 0);
            OutboundSpeedText = TrafficFormat.Speed(snapshot.BytesOut > 0 ? snapshot.BytesPerSecond / 2 : 0);
            TotalTrafficText = TrafficFormat.Bytes(snapshot.TotalBytes);

            Tunnels.Clear();
            foreach (var (name, channelSnapshot) in runtime.GetChannelSnapshots().OrderByDescending(x => x.Snapshot.TotalBytes))
            {
                Tunnels.Add(new TunnelTrafficItem(
                    name,
                    $"入 {TrafficFormat.Bytes(channelSnapshot.BytesIn)} · 出 {TrafficFormat.Bytes(channelSnapshot.BytesOut)}"));
            }
        }

        OnPropertyChanged(nameof(IsRunning));
        await Task.CompletedTask;
    }
}
