using System.Collections.Concurrent;

namespace LightingProxy.Core.Telemetry;

public sealed class ProxyRuntimeTracker
{
    private readonly ConcurrentDictionary<string, TrafficMeter> _channels = new(StringComparer.OrdinalIgnoreCase);
    private readonly TrafficMeter _total = new();

    public TrafficMeter Total => _total;

    public TrafficMeter GetOrCreateChannel(string channelName)
        => _channels.GetOrAdd(channelName, _ => new TrafficMeter());

    public IReadOnlyDictionary<string, TrafficMeter> Channels => _channels;

    public TrafficSnapshot GetTotalSnapshot() => _total.GetSnapshot();

    public IEnumerable<(string Name, TrafficSnapshot Snapshot)> GetChannelSnapshots()
        => _channels.Select(pair => (pair.Key, pair.Value.GetSnapshot()));
}
