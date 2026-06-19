namespace LightingProxy.Core.Telemetry;

public static class TrafficRelay
{
    public static async Task BidirectionalAsync(
        ProxyRuntimeTracker tracker,
        string channel,
        Stream first,
        Stream second,
        CancellationToken cancellationToken = default)
    {
        var meter = new CompositeTrafficRecorder(tracker.Total, tracker.GetOrCreateChannel(channel));
        using var _ = TrafficScope.Begin(meter);
        await Transport.StreamRelay.RelayBidirectionalAsync(first, second, cancellationToken).ConfigureAwait(false);
    }
}
