using LightingProxy.Core.Telemetry;
using LightingProxy.Windows.Shared.Helpers;
using LightingProxy.Windows.Shared.Services;

namespace LightingProxy.CLI.Shared;

public static class CliStatusConsole
{
    public static void PrintClientStatus(ClientHostService host)
    {
        var state = FormatConnectionState(host.ConnectionState);
        var uptime = host.ConnectedAt is null
            ? "--"
            : TrafficFormat.Duration(DateTimeOffset.UtcNow - host.ConnectedAt.Value);
        var heartbeat = host.LastHeartbeatUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 状态: {state} | 在线: {uptime} | 最近心跳: {heartbeat}");

        if (!string.IsNullOrWhiteSpace(host.LastError))
        {
            Console.WriteLine($"  最近错误: {host.LastError}");
        }

        var runtime = host.Runtime;
        if (runtime is null)
        {
            Console.WriteLine("  流量: 暂无数据");
            return;
        }

        var snapshot = runtime.GetTotalSnapshot();
        var inboundSpeed = TrafficFormat.Speed(snapshot.BytesIn > 0 ? snapshot.BytesPerSecond / 2 : 0);
        var outboundSpeed = TrafficFormat.Speed(snapshot.BytesOut > 0 ? snapshot.BytesPerSecond / 2 : 0);
        Console.WriteLine($"  上行: {outboundSpeed} | 下行: {inboundSpeed} | 累计: {TrafficFormat.Bytes(snapshot.TotalBytes)}");

        foreach (var (name, channelSnapshot) in runtime.GetChannelSnapshots().OrderByDescending(x => x.Snapshot.TotalBytes))
        {
            Console.WriteLine($"  - {name}: 入 {TrafficFormat.Bytes(channelSnapshot.BytesIn)} · 出 {TrafficFormat.Bytes(channelSnapshot.BytesOut)}");
        }
    }

    public static void PrintServerStatus(ServerHostService host)
    {
        var state = FormatConnectionState(host.ConnectionState);
        var uptime = host.StartedAt is null
            ? "--"
            : TrafficFormat.Duration(DateTimeOffset.UtcNow - host.StartedAt.Value);

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 状态: {state} | 运行: {uptime}");

        if (!string.IsNullOrWhiteSpace(host.LastError))
        {
            Console.WriteLine($"  最近错误: {host.LastError}");
        }

        var server = host.Server;
        if (server is null)
        {
            Console.WriteLine("  会话: 暂无数据");
            return;
        }

        var aggregateIn = 0L;
        var aggregateOut = 0L;
        var aggregateRate = 0d;

        foreach (var session in server.Sessions)
        {
            var snapshot = session.Runtime.GetTotalSnapshot();
            aggregateIn += snapshot.BytesIn;
            aggregateOut += snapshot.BytesOut;
            aggregateRate += snapshot.BytesPerSecond;
        }

        Console.WriteLine($"  活跃会话: {server.Sessions.Count}");
        Console.WriteLine($"  上行: {TrafficFormat.Speed(aggregateRate / 2)} | 下行: {TrafficFormat.Speed(aggregateRate / 2)} | 累计: {TrafficFormat.Bytes(aggregateIn + aggregateOut)}");

        foreach (var session in server.Sessions)
        {
            var snapshot = session.Runtime.GetTotalSnapshot();
            Console.WriteLine($"  - 会话 {session.SessionId}: 心跳 {session.LastHeartbeatUtc.ToLocalTime():HH:mm:ss} · 流量 {TrafficFormat.Bytes(snapshot.TotalBytes)}");
        }
    }

    private static string FormatConnectionState(ProxyConnectionState state)
        => state switch
        {
            ProxyConnectionState.Connected => "在线",
            ProxyConnectionState.Starting => "连接中",
            ProxyConnectionState.Faulted => "异常",
            _ => "已停止"
        };
}
