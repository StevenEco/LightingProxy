namespace LightingProxy.Domain.Common;

public class BandwidthConfig
{
    /// <summary>
    /// 带宽限制，如 "1MB"、"512KB"。
    /// </summary>
    public string? Limit { get; set; }
}
