using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Configuration;

public class ConfigurationSourceOptions
{
    public ConfigurationStorageKind Storage { get; set; } = ConfigurationStorageKind.File;

    public ConfigurationFileFormat FileFormat { get; set; } = ConfigurationFileFormat.Json;

    public string? FilePath { get; set; }

    public DatabaseConfigurationOptions? Database { get; set; }

    public string Name { get; set; } = "default";

    /// <summary>
    /// 文件存储时指定配置类型。数据库模式下可忽略。
    /// </summary>
    public ConfigurationKind? TargetKind { get; set; }

    public bool Validate { get; set; } = true;
}
