using System.Text.Json;
using LightingProxy.Domain.Client;
using LightingProxy.Domain.Configuration;
using LightingProxy.Domain.Enums;
using LightingProxy.Windows.Shared.Services;

namespace LightingProxy.CLI.Shared;

public static class ClientCliHost
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task<int> RunAsync(string[] args)
    {
        CliParseResult parseResult;
        try
        {
            parseResult = CliOptionsParser.Parse(args, "lightingproxy-client");
        }
        catch (CliParseException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return 1;
        }

        if (!parseResult.Succeeded || parseResult.Options is null)
        {
            await Console.Error.WriteLineAsync(parseResult.ErrorMessage ?? "参数解析失败。").ConfigureAwait(false);
            return 1;
        }

        var options = parseResult.Options;
        if (options.Command is CliCommand.Help)
        {
            CliHelpText.WriteClientHelp();
            return 0;
        }

        var context = new CliConfigurationContext("client", ConfigurationKind.Client);
        var preferences = context.BuildPreferences(options);
        var sourceOptions = context.ToSourceOptions(preferences);
        var configurationService = new ConfigurationAppService();

        try
        {
            return options.Command switch
            {
                CliCommand.Show => await ShowAsync(configurationService, sourceOptions).ConfigureAwait(false),
                CliCommand.Save => await SaveAsync(configurationService, sourceOptions).ConfigureAwait(false),
                CliCommand.Validate => await ValidateAsync(configurationService, sourceOptions).ConfigureAwait(false),
                _ => await RunServiceAsync(configurationService, sourceOptions, options).ConfigureAwait(false)
            };
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"执行失败: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> ShowAsync(ConfigurationAppService configurationService, ConfigurationSourceOptions sourceOptions)
    {
        var config = await configurationService.LoadClientAsync(sourceOptions).ConfigureAwait(false)
                     ?? configurationService.CreateDefaultClientConfig();
        Console.WriteLine(JsonSerializer.Serialize(config, JsonOptions));
        return 0;
    }

    private static async Task<int> SaveAsync(ConfigurationAppService configurationService, ConfigurationSourceOptions sourceOptions)
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        var json = await reader.ReadToEndAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            await Console.Error.WriteLineAsync("save 命令需要从标准输入提供 JSON 配置。").ConfigureAwait(false);
            return 1;
        }

        var config = JsonSerializer.Deserialize<ClientConfig>(json)
                     ?? throw new InvalidOperationException("客户端配置 JSON 无效。");
        await configurationService.SaveClientAsync(config, sourceOptions).ConfigureAwait(false);
        Console.WriteLine("配置已保存。");
        return 0;
    }

    private static async Task<int> ValidateAsync(ConfigurationAppService configurationService, ConfigurationSourceOptions sourceOptions)
    {
        var config = await configurationService.LoadClientAsync(sourceOptions).ConfigureAwait(false);
        if (config is null)
        {
            await Console.Error.WriteLineAsync("未找到客户端配置。").ConfigureAwait(false);
            return 1;
        }

        Console.WriteLine("客户端配置校验通过。");
        return 0;
    }

    private static async Task<int> RunServiceAsync(
        ConfigurationAppService configurationService,
        ConfigurationSourceOptions sourceOptions,
        CliOptions options)
    {
        var config = await configurationService.LoadClientAsync(sourceOptions).ConfigureAwait(false)
                     ?? configurationService.CreateDefaultClientConfig();

        await using var hostService = new ClientHostService();
        using var shutdown = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdown.Cancel();

        Console.WriteLine("正在启动 LightingProxy 客户端...");
        Console.WriteLine($"配置源: {DescribeSource(sourceOptions)}");
        Console.WriteLine("按 Ctrl+C 停止。");
        Console.WriteLine();

        try
        {
            await hostService.StartAsync(config, shutdown.Token).ConfigureAwait(false);
            Console.WriteLine("隧道客户端已连接。");
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"启动失败: {ex.Message}").ConfigureAwait(false);
            return 1;
        }

        try
        {
            while (!shutdown.IsCancellationRequested)
            {
                CliStatusConsole.PrintClientStatus(hostService);
                Console.WriteLine(new string('-', 60));
                await Task.Delay(TimeSpan.FromSeconds(options.StatusIntervalSeconds), shutdown.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
        finally
        {
            Console.WriteLine();
            Console.WriteLine("正在停止客户端...");
            await hostService.StopAsync().ConfigureAwait(false);
            Console.WriteLine("客户端已停止。");
        }

        return 0;
    }

    private static string DescribeSource(ConfigurationSourceOptions sourceOptions)
        => sourceOptions.Storage == ConfigurationStorageKind.Database
            ? $"database ({sourceOptions.Database?.Provider}, name={sourceOptions.Name})"
            : $"file ({sourceOptions.FileFormat}, {sourceOptions.FilePath})";
}
