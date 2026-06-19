namespace LightingProxy.CLI.Shared;

public static class CliHelpText
{
    public static void WriteClientHelp()
    {
        Console.WriteLine(
            """
            LightingProxy 客户端 CLI

            用法:
              lightingproxy-client [command] [options]

            命令:
              run        启动隧道客户端（默认）
              show       输出当前配置（JSON）
              save       从标准输入读取 JSON 并保存
              validate   校验配置后退出
              help       显示帮助

            选项:
              -c, --config <PATH>           配置文件路径（JSON 或 INI）
              --storage <file|database>   配置存储方式（默认: file）
              --format <json|ini>           配置文件格式（未指定时按扩展名推断）
              --name, --config-name <NAME>  数据库模式下的配置名称（默认: default）
              --db-provider <PROVIDER>      sqlite | sqlserver | mysql | postgresql
              --connection-string <CS>      数据库连接字符串
              --auto-create-db              自动创建数据库结构
              --no-auto-create-db           不自动创建数据库结构
              --no-validate                 保存时跳过校验
              --status-interval <SECONDS>   状态刷新间隔（默认: 2）
              -h, --help                    显示帮助

            示例:
              lightingproxy-client run -c config_examples/client.json
              lightingproxy-client show -c ./client.ini --format ini
              lightingproxy-client run --storage database --db-provider sqlite --connection-string "Data Source=client.db"
            """);
    }

    public static void WriteServerHelp()
    {
        Console.WriteLine(
            """
            LightingProxy 服务端 CLI

            用法:
              lightingproxy-server [command] [options]

            命令:
              run        启动代理服务端（默认）
              show       输出当前配置（JSON）
              save       从标准输入读取 JSON 并保存
              validate   校验配置后退出
              help       显示帮助

            选项:
              -c, --config <PATH>           配置文件路径（JSON 或 INI）
              --storage <file|database>   配置存储方式（默认: file）
              --format <json|ini>           配置文件格式（未指定时按扩展名推断）
              --name, --config-name <NAME>  数据库模式下的配置名称（默认: default）
              --db-provider <PROVIDER>      sqlite | sqlserver | mysql | postgresql
              --connection-string <CS>      数据库连接字符串
              --auto-create-db              自动创建数据库结构
              --no-auto-create-db           不自动创建数据库结构
              --no-validate                 保存时跳过校验
              --status-interval <SECONDS>   状态刷新间隔（默认: 2）
              -h, --help                    显示帮助

            示例:
              lightingproxy-server run -c config_examples/server.json
              lightingproxy-server show -c ./server.ini --format ini
              lightingproxy-server run --storage database --db-provider sqlite --connection-string "Data Source=server.db"
            """);
    }
}
