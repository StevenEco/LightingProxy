using LightingProxy.Domain.Enums;

namespace LightingProxy.CLI.Shared;

public sealed class CliOptions
{
    public CliCommand Command { get; init; } = CliCommand.Run;

    public string? ConfigPath { get; init; }

    public ConfigurationStorageKind? Storage { get; init; }

    public ConfigurationFileFormat? FileFormat { get; init; }

    public string? ConfigName { get; init; }

    public DatabaseProvider? DatabaseProvider { get; init; }

    public string? ConnectionString { get; init; }

    public bool? AutoCreateDatabase { get; init; }

    public bool ValidateOnSave { get; init; } = true;

    public int StatusIntervalSeconds { get; init; } = 2;
}
