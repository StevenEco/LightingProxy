namespace LightingProxy.Core.Telemetry;

public sealed class TrafficMeter : ITrafficRecorder
{
    private long _bytesIn;
    private long _bytesOut;
    private long _lastSampleTotal;
    private DateTimeOffset _lastSampleUtc = DateTimeOffset.UtcNow;

    public void RecordInbound(long bytes)
    {
        if (bytes > 0)
        {
            Interlocked.Add(ref _bytesIn, bytes);
        }
    }

    public void RecordOutbound(long bytes)
    {
        if (bytes > 0)
        {
            Interlocked.Add(ref _bytesOut, bytes);
        }
    }

    public TrafficSnapshot GetSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        var bytesIn = Interlocked.Read(ref _bytesIn);
        var bytesOut = Interlocked.Read(ref _bytesOut);
        var total = bytesIn + bytesOut;
        var elapsed = Math.Max((now - _lastSampleUtc).TotalSeconds, 0.001);
        var delta = total - _lastSampleTotal;
        _lastSampleTotal = total;
        _lastSampleUtc = now;
        var bytesPerSecond = delta / elapsed;
        return new TrafficSnapshot(bytesIn, bytesOut, total, bytesPerSecond);
    }
}
