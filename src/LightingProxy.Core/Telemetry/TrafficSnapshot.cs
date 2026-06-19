namespace LightingProxy.Core.Telemetry;

public sealed class TrafficSnapshot
{
    public TrafficSnapshot(long bytesIn, long bytesOut, long totalBytes, double bytesPerSecond)
    {
        BytesIn = bytesIn;
        BytesOut = bytesOut;
        TotalBytes = totalBytes;
        BytesPerSecond = bytesPerSecond;
    }

    public long BytesIn { get; }

    public long BytesOut { get; }

    public long TotalBytes { get; }

    public double BytesPerSecond { get; }
}
