using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Common;

public class HealthCheckConfig
{
    public HealthCheckType Type { get; set; } = HealthCheckType.Tcp;

    public string? Path { get; set; }

    public int TimeoutSeconds { get; set; } = 3;

    public int MaxFailed { get; set; } = 3;

    public int IntervalSeconds { get; set; } = 10;
}
