using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Configuration;

public class DatabaseConfigurationOptions
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    public required string ConnectionString { get; set; }

    public bool AutoCreateDatabase { get; set; } = true;
}
