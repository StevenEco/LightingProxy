namespace LightingProxy.Core.Telemetry;

public static class TrafficScope
{
    private static readonly AsyncLocal<ITrafficRecorder?> CurrentMeter = new();

    public static ITrafficRecorder? Current => CurrentMeter.Value;

    public static IDisposable Begin(ITrafficRecorder meter)
    {
        ArgumentNullException.ThrowIfNull(meter);
        var previous = CurrentMeter.Value;
        CurrentMeter.Value = meter;
        return new Scope(previous);
    }

    private sealed class Scope(ITrafficRecorder? previous) : IDisposable
    {
        public void Dispose() => CurrentMeter.Value = previous;
    }
}
