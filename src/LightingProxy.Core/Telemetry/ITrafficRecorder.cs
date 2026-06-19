namespace LightingProxy.Core.Telemetry;

public interface ITrafficRecorder
{
    void RecordInbound(long bytes);

    void RecordOutbound(long bytes);
}
