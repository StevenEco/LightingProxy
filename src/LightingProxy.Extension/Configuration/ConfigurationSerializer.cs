using System.Text.Json;
using LightingProxy.Domain.Client;
using LightingProxy.Domain.Server;
using LightingProxy.Extension.Json;
using LightingProxy.Extension.Validation;

namespace LightingProxy.Extension.Configuration;

public static class ConfigurationSerializer
{
    private static readonly JsonSerializerOptions ReadOptions = JsonConfigurationOptions.Create();
    private static readonly JsonSerializerOptions WriteOptions = JsonConfigurationOptions.Create(writeIndented: true);

    public static ServerConfig DeserializeServerConfig(string json, bool validate = true, string? source = null)
    {
        try
        {
            var config = JsonSerializer.Deserialize<ServerConfig>(json, ReadOptions)
                ?? throw CreateDeserializeException<ServerConfig>(source);

            if (validate)
            {
                EnsureValid(ConfigurationValidator.Validate(config));
            }

            return config;
        }
        catch (JsonException ex)
        {
            throw WrapJsonException(ex, source);
        }
    }

    public static ClientConfig DeserializeClientConfig(string json, bool validate = true, string? source = null)
    {
        try
        {
            var config = JsonSerializer.Deserialize<ClientConfig>(json, ReadOptions)
                ?? throw CreateDeserializeException<ClientConfig>(source);

            if (validate)
            {
                EnsureValid(ConfigurationValidator.Validate(config));
            }

            return config;
        }
        catch (JsonException ex)
        {
            throw WrapJsonException(ex, source);
        }
    }

    public static string SerializeServerConfig(ServerConfig config, bool validate = true)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (validate)
        {
            EnsureValid(ConfigurationValidator.Validate(config));
        }

        return JsonSerializer.Serialize(config, WriteOptions);
    }

    public static string SerializeClientConfig(ClientConfig config, bool validate = true)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (validate)
        {
            EnsureValid(ConfigurationValidator.Validate(config));
        }

        return JsonSerializer.Serialize(config, WriteOptions);
    }

    private static void EnsureValid(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(errors);
        }
    }

    private static InvalidOperationException CreateDeserializeException<T>(string? source)
    {
        return source is null
            ? new InvalidOperationException($"Failed to deserialize {typeof(T).Name}: content is empty.")
            : new InvalidOperationException($"Failed to deserialize {typeof(T).Name} from '{source}': content is empty.");
    }

    private static InvalidOperationException WrapJsonException(JsonException exception, string? source)
    {
        var message = source is null
            ? "Configuration content contains invalid JSON."
            : $"Configuration source '{source}' contains invalid JSON.";

        return new InvalidOperationException(message, exception);
    }
}
