using LightingProxy.Domain.Configuration;
using LightingProxy.Domain.Persistence;
using LightingProxy.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LightingProxy.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLightingProxyConfiguration(
        this IServiceCollection services,
        ConfigurationSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<IConfigurationStore>(_ => ConfigurationProvider.CreateStore(options));
        return services;
    }
}
