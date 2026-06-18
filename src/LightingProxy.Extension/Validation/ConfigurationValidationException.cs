namespace LightingProxy.Extension.Validation;

public sealed class ConfigurationValidationException : Exception
{
    public ConfigurationValidationException(IReadOnlyList<string> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }

    private static string BuildMessage(IReadOnlyList<string> errors)
    {
        return errors.Count == 1
            ? errors[0]
            : $"Configuration validation failed with {errors.Count} error(s):{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors)}";
    }
}
