using LightingProxy.Domain.Enums;

namespace LightingProxy.Infrastructure.Persistence.Entities;

public class ConfigurationRecord
{
    public long Id { get; set; }

    public string Name { get; set; } = "default";

    public ConfigurationKind Kind { get; set; }

    public string Content { get; set; } = "{}";

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
