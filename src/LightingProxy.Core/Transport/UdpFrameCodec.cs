using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace LightingProxy.Core.Transport;

public static class UdpFrameCodec
{
    public static byte[] Encode(string? remoteKey, ReadOnlySpan<byte> payload)
    {
        var remoteBytes = remoteKey is null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(remoteKey);
        var buffer = new byte[2 + remoteBytes.Length + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, (ushort)remoteBytes.Length);
        remoteBytes.CopyTo(buffer.AsSpan(2));
        payload.CopyTo(buffer.AsSpan(2 + remoteBytes.Length));
        return buffer;
    }

    public static bool TryDecode(ReadOnlySpan<byte> frame, out string? remoteKey, out ReadOnlySpan<byte> payload)
    {
        remoteKey = null;
        payload = ReadOnlySpan<byte>.Empty;

        if (frame.Length < 2)
        {
            return false;
        }

        var remoteLength = BinaryPrimitives.ReadUInt16LittleEndian(frame);
        if (frame.Length < 2 + remoteLength)
        {
            return false;
        }

        if (remoteLength > 0)
        {
            remoteKey = Encoding.UTF8.GetString(frame.Slice(2, remoteLength));
        }

        payload = frame.Slice(2 + remoteLength);
        return true;
    }

    public static string FormatEndpoint(IPEndPoint endpoint) => $"{endpoint.Address}:{endpoint.Port}";

    public static IPEndPoint ParseEndpoint(string value)
    {
        var index = value.LastIndexOf(':');
        var host = value[..index];
        var port = int.Parse(value[(index + 1)..]);
        return new IPEndPoint(IPAddress.Parse(host), port);
    }
}
