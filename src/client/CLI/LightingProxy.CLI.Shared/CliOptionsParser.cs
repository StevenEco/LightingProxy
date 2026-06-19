using LightingProxy.Domain.Enums;

namespace LightingProxy.CLI.Shared;

public static class CliOptionsParser
{
    public static CliParseResult Parse(string[] args, string appDisplayName)
    {
        if (args.Length == 0)
        {
            return CliParseResult.Ok(new CliOptions());
        }

        var command = CliCommand.Run;
        var index = 0;

        if (!args[0].StartsWith('-'))
        {
            command = ParseCommand(args[0]);
            index = 1;
        }

        string? configPath = null;
        ConfigurationStorageKind? storage = null;
        ConfigurationFileFormat? fileFormat = null;
        string? configName = null;
        DatabaseProvider? databaseProvider = null;
        string? connectionString = null;
        bool? autoCreateDatabase = null;
        var validateOnSave = true;
        var statusIntervalSeconds = 2;

        for (; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "-h":
                case "--help":
                    return CliParseResult.Ok(new CliOptions { Command = CliCommand.Help });

                case "-c":
                case "--config":
                    configPath = RequireValue(args, ref index, arg);
                    break;

                case "--storage":
                    storage = ParseStorage(RequireValue(args, ref index, arg));
                    break;

                case "--format":
                    fileFormat = ParseFileFormat(RequireValue(args, ref index, arg));
                    break;

                case "--name":
                case "--config-name":
                    configName = RequireValue(args, ref index, arg);
                    break;

                case "--db-provider":
                    databaseProvider = ParseDatabaseProvider(RequireValue(args, ref index, arg));
                    break;

                case "--connection-string":
                    connectionString = RequireValue(args, ref index, arg);
                    break;

                case "--auto-create-db":
                    autoCreateDatabase = true;
                    break;

                case "--no-auto-create-db":
                    autoCreateDatabase = false;
                    break;

                case "--no-validate":
                    validateOnSave = false;
                    break;

                case "--status-interval":
                    statusIntervalSeconds = ParsePositiveInt(RequireValue(args, ref index, arg), arg);
                    break;

                default:
                    return CliParseResult.Fail($"未知参数: {arg}。使用 {appDisplayName} --help 查看帮助。");
            }
        }

        return CliParseResult.Ok(new CliOptions
        {
            Command = command,
            ConfigPath = configPath,
            Storage = storage,
            FileFormat = fileFormat,
            ConfigName = configName,
            DatabaseProvider = databaseProvider,
            ConnectionString = connectionString,
            AutoCreateDatabase = autoCreateDatabase,
            ValidateOnSave = validateOnSave,
            StatusIntervalSeconds = statusIntervalSeconds
        });
    }

    private static CliCommand ParseCommand(string value)
        => value.ToLowerInvariant() switch
        {
            "run" => CliCommand.Run,
            "show" => CliCommand.Show,
            "save" => CliCommand.Save,
            "validate" => CliCommand.Validate,
            "help" => CliCommand.Help,
            _ => throw new CliParseException($"未知命令: {value}")
        };

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new CliParseException($"参数 {optionName} 需要指定值。");
        }

        index++;
        return args[index];
    }

    private static int ParsePositiveInt(string value, string optionName)
    {
        if (!int.TryParse(value, out var parsed) || parsed <= 0)
        {
            throw new CliParseException($"参数 {optionName} 必须是正整数。");
        }

        return parsed;
    }

    private static ConfigurationStorageKind ParseStorage(string value)
        => value.ToLowerInvariant() switch
        {
            "file" => ConfigurationStorageKind.File,
            "database" => ConfigurationStorageKind.Database,
            "db" => ConfigurationStorageKind.Database,
            _ => throw new CliParseException($"不支持的存储类型: {value}。可用值: file, database")
        };

    private static ConfigurationFileFormat ParseFileFormat(string value)
        => value.ToLowerInvariant() switch
        {
            "json" => ConfigurationFileFormat.Json,
            "ini" => ConfigurationFileFormat.Ini,
            _ => throw new CliParseException($"不支持的文件格式: {value}。可用值: json, ini")
        };

    private static DatabaseProvider ParseDatabaseProvider(string value)
        => value.ToLowerInvariant() switch
        {
            "sqlite" => DatabaseProvider.Sqlite,
            "sqlserver" => DatabaseProvider.SqlServer,
            "mssql" => DatabaseProvider.SqlServer,
            "mysql" => DatabaseProvider.MySql,
            "postgresql" => DatabaseProvider.PostgreSql,
            "postgres" => DatabaseProvider.PostgreSql,
            _ => throw new CliParseException($"不支持的数据库提供程序: {value}。可用值: sqlite, sqlserver, mysql, postgresql")
        };
}

public sealed class CliParseResult
{
    private CliParseResult(bool succeeded, CliOptions? options, string? errorMessage)
    {
        Succeeded = succeeded;
        Options = options;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    public CliOptions? Options { get; }

    public string? ErrorMessage { get; }

    public static CliParseResult Ok(CliOptions options) => new(true, options, null);

    public static CliParseResult Fail(string message) => new(false, null, message);
}

public sealed class CliParseException(string message) : Exception(message);
