namespace LightingProxy.Core.Telemetry;

public sealed class CompositeTrafficRecorder : ITrafficRecorder
{
    private readonly ITrafficRecorder[] _recorders;

    public CompositeTrafficRecorder(params ITrafficRecorder[] recorders)
    {
        _recorders = recorders ?? throw new ArgumentNullException(nameof(recorders));
    }

    public void RecordInbound(long bytes)
    {
        foreach (var recorder in _recorders)
        {
            recorder.RecordInbound(bytes);
        }
    }

    public void RecordOutbound(long bytes)
    {
        foreach (var recorder in _recorders)
        {
            recorder.RecordOutbound(bytes);
        }
    }
}
