using LightingProxy.Domain.Configuration;
using LightingProxy.Domain.Enums;
using LightingProxy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LightingProxy.Infrastructure.Persistence;

public static class DbContextFactory
{
    public static LightingProxyDbContext Create(DatabaseConfigurationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("Database connection string is required.");
        }

        var builder = new DbContextOptionsBuilder<LightingProxyDbContext>();
        ConfigureProvider(builder, options.Provider, options.ConnectionString);
        return new LightingProxyDbContext(builder.Options);
    }

    public static void ConfigureProvider(
        DbContextOptionsBuilder<LightingProxyDbContext> builder,
        DatabaseProvider provider,
        string connectionString)
    {
        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                builder.UseSqlite(connectionString);
                break;
            case DatabaseProvider.SqlServer:
                builder.UseSqlServer(connectionString);
                break;
            case DatabaseProvider.PostgreSql:
                builder.UseNpgsql(connectionString);
                break;
            case DatabaseProvider.MySql:
                builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                break;
            default:
                throw new NotSupportedException($"Unsupported database provider: {provider}");
        }
    }
}
