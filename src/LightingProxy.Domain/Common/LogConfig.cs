using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Common;

public class LogConfig
{
    public LogTarget Target { get; set; } = LogTarget.Console;

    public string? FilePath { get; set; }

    public LogLevel Level { get; set; } = LogLevel.Info;

    public int MaxDays { get; set; } = 3;
}
