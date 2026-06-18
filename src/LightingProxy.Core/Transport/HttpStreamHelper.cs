using System.Text;

namespace LightingProxy.Core.Transport;

internal static class HttpStreamHelper
{
    public static async Task<(string? Host, byte[] Headers, byte[] Tail)> ReadHttpPartsAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[1024];

        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
            var data = buffer.ToArray();
            var headerEnd = IndexOf(data, "\r\n\r\n"u8);
            if (headerEnd >= 0)
            {
                var headerLength = headerEnd + 4;
                var host = ParseHost(data.AsSpan(0, headerEnd));
                var headers = data.AsSpan(0, headerLength).ToArray();
                var tail = data.AsSpan(headerLength).ToArray();
                return (host, headers, tail);
            }
        }

        var fallback = buffer.ToArray();
        return (null, fallback, []);
    }

    public static async Task<(string? Host, byte[] Prefix)> ReadHttpPrefixAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var (host, headers, tail) = await ReadHttpPartsAsync(stream, cancellationToken).ConfigureAwait(false);
        if (tail.Length == 0)
        {
            return (host, headers);
        }

        var prefix = new byte[headers.Length + tail.Length];
        headers.CopyTo(prefix, 0);
        tail.CopyTo(prefix, headers.Length);
        return (host, prefix);
    }

    public static string? ParseConnectHost(ReadOnlySpan<byte> prefix)
    {
        var text = Encoding.ASCII.GetString(prefix);
        var firstLineEnd = text.IndexOf("\r\n", StringComparison.Ordinal);
        if (firstLineEnd < 0)
        {
            return null;
        }

        var requestLine = text[..firstLineEnd];
        if (!requestLine.StartsWith("CONNECT ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var target = requestLine["CONNECT ".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return target.Split(':')[0];
    }

    private static string? ParseHost(ReadOnlySpan<byte> headers)
    {
        var text = Encoding.ASCII.GetString(headers);
        foreach (var line in text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var host = line["Host:".Length..].Trim();
            var colonIndex = host.IndexOf(':');
            return colonIndex >= 0 ? host[..colonIndex] : host;
        }

        return null;
    }

    private static int IndexOf(ReadOnlySpan<byte> data, ReadOnlySpan<byte> pattern)
    {
        if (pattern.Length == 0 || data.Length < pattern.Length)
        {
            return -1;
        }

        for (var i = 0; i <= data.Length - pattern.Length; i++)
        {
            if (data.Slice(i, pattern.Length).SequenceEqual(pattern))
            {
                return i;
            }
        }

        return -1;
    }
}
