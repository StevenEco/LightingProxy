namespace LightingProxy.Extension.Configuration.Ini;

internal static class IniDocument
{
    public static Dictionary<string, Dictionary<string, string>> Parse(string content)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? current = null;
        var currentName = string.Empty;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentName = line[1..^1].Trim();
                if (!sections.TryGetValue(currentName, out current))
                {
                    current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    sections[currentName] = current;
                }

                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            current ??= GetOrCreateSection(sections, currentName);
            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
            {
                value = value[1..^1];
            }

            current[key] = value;
        }

        return sections;
    }

    public static string Serialize(IDictionary<string, Dictionary<string, string?>> sections)
    {
        using var writer = new StringWriter();

        foreach (var section in sections)
        {
            writer.WriteLine($"[{section.Key}]");

            foreach (var entry in section.Value)
            {
                if (string.IsNullOrEmpty(entry.Value))
                {
                    continue;
                }

                writer.WriteLine($"{entry.Key} = {entry.Value}");
            }

            writer.WriteLine();
        }

        return writer.ToString().TrimEnd();
    }

    private static Dictionary<string, string> GetOrCreateSection(
        Dictionary<string, Dictionary<string, string>> sections,
        string name)
    {
        if (!sections.TryGetValue(name, out var section))
        {
            section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            sections[name] = section;
        }

        return section;
    }
}
