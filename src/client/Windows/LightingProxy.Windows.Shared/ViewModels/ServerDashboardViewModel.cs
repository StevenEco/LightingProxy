using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightingProxy.Core.Telemetry;
using LightingProxy.Windows.Shared.Helpers;
using LightingProxy.Windows.Shared.Services;

namespace LightingProxy.Windows.Shared.ViewModels;

public partial class ServerDashboardViewModel : ObservableObject
{
    private readonly ServerHostService _hostService;

    public ServerDashboardViewModel(ServerHostService hostService)
    {
        _hostService = hostService;
        _hostService.StateChanged += (_, _) => _ = RefreshAsync();
    }

    [ObservableProperty]
    private string _statusTitle = "服务未启动";

    [ObservableProperty]
    private string _statusSubtitle = "等待启动代理服务";

    [ObservableProperty]
    private string _uptimeText = "--";

    [ObservableProperty]
    private string _inboundSpeedText = "0 B/s";

    [ObservableProperty]
    private string _outboundSpeedText = "0 B/s";

    [ObservableProperty]
    private string _totalTrafficText = "0 B";

    [ObservableProperty]
    private int _activeSessions;

    public ObservableCollection<TunnelTrafficItem> Sessions { get; } = [];

    public bool IsRunning => _hostService.ConnectionState == ProxyConnectionState.Connected
                             || _hostService.ConnectionState == ProxyConnectionState.Starting;

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
            ProxyConnectionState.Connected => "服务运行中",
            ProxyConnectionState.Starting => "正在启动",
            ProxyConnectionState.Faulted => "服务异常",
            _ => "服务未启动"
        };

        StatusSubtitle = _hostService.ConnectionState switch
        {
            ProxyConnectionState.Connected => "控制端口监听中，等待客户端接入",
            ProxyConnectionState.Starting => "正在绑定端口并初始化共享监听器",
            ProxyConnectionState.Faulted => _hostService.LastError ?? "请检查端口占用与配置",
            _ => "在配置页应用设置后自动启动服务"
        };

        UptimeText = _hostService.StartedAt is null
            ? "--"
            : TrafficFormat.Duration(DateTimeOffset.UtcNow - _hostService.StartedAt.Value);

        var server = _hostService.Server;
        if (server is not null)
        {
            ActiveSessions = server.Sessions.Count;
            var aggregateIn = 0L;
            var aggregateOut = 0L;
            var aggregateRate = 0d;

            Sessions.Clear();
            foreach (var session in server.Sessions)
            {
                var snapshot = session.Runtime.GetTotalSnapshot();
                aggregateIn += snapshot.BytesIn;
                aggregateOut += snapshot.BytesOut;
                aggregateRate += snapshot.BytesPerSecond;

                Sessions.Add(new TunnelTrafficItem(
                    $"会话 {session.SessionId}",
                    $"心跳 {session.LastHeartbeatUtc.ToLocalTime():HH:mm:ss} · 流量 {TrafficFormat.Bytes(snapshot.TotalBytes)}"));
            }

            InboundSpeedText = TrafficFormat.Speed(aggregateRate / 2);
            OutboundSpeedText = TrafficFormat.Speed(aggregateRate / 2);
            TotalTrafficText = TrafficFormat.Bytes(aggregateIn + aggregateOut);
        }

        OnPropertyChanged(nameof(IsRunning));
        await Task.CompletedTask;
    }
}
