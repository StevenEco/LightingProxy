namespace LightingProxy.Windows.Shared.Helpers;

public static class TrafficFormat
{
    public static string Bytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{size:0} {units[unit]}" : $"{size:0.##} {units[unit]}";
    }

    public static string Speed(double bytesPerSecond)
        => $"{Bytes((long)bytesPerSecond)}/s";

    public static string Duration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            return $"{(int)duration.TotalDays}天 {duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        }

        return duration.ToString(duration.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");
    }
}
