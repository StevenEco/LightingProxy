using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LightingProxy.Core.Protocol;

public static class MessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task WriteAsync(Stream stream, ControlMessage message, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, Options);
        var lengthBytes = BitConverter.GetBytes(json.Length);
        await stream.WriteAsync(lengthBytes, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(json, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<ControlMessage?> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var lengthBuffer = new byte[4];
        await ReadExactAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);

        if (length <= 0 || length > 1024 * 1024)
        {
            throw new InvalidOperationException($"Invalid message length: {length}");
        }

        var payload = new byte[length];
        await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ControlMessage>(payload, Options);
    }

    public static ControlMessage CreateLogin(string token)
        => new() { Type = MessageType.Login, Token = token };

    public static ControlMessage CreateLoginResp(bool success, string? error = null)
        => new() { Type = MessageType.LoginResp, Success = success, Error = error };

    public static ControlMessage CreateNewProxy(string name, string protocol, Dictionary<string, string>? metadata = null)
        => new() { Type = MessageType.NewProxy, ProxyName = name, Protocol = protocol, Metadata = metadata };

    public static ControlMessage CreateNewProxyResp(string name, bool success, string? error = null)
        => new() { Type = MessageType.NewProxyResp, ProxyName = name, Success = success, Error = error };

    public static ControlMessage CreateNewVisitor(string visitorName, string serverName, string secretKey)
        => new() { Type = MessageType.NewVisitor, VisitorName = visitorName, ServerName = serverName, SecretKey = secretKey };

    public static ControlMessage CreateNewVisitorResp(string visitorName, bool success, string? error = null)
        => new() { Type = MessageType.NewVisitorResp, VisitorName = visitorName, Success = success, Error = error };

    public static ControlMessage CreateReqWorkConn(Guid requestId, string proxyName, Dictionary<string, string>? metadata = null)
        => new() { Type = MessageType.ReqWorkConn, RequestId = requestId, ProxyName = proxyName, Metadata = metadata };

    public static ControlMessage CreateStartWorkConn(string proxyName, Dictionary<string, string>? metadata = null)
        => new() { Type = MessageType.StartWorkConn, ProxyName = proxyName, Metadata = metadata };

    public static ControlMessage CreateWorkConnReady(Guid requestId)
        => new() { Type = MessageType.WorkConnReady, RequestId = requestId };

    public static ControlMessage CreateWorkConnStarted()
        => new() { Type = MessageType.WorkConnStarted };

    public static ControlMessage CreatePing()
        => new() { Type = MessageType.Ping };

    public static ControlMessage CreatePong()
        => new() { Type = MessageType.Pong };

    public static ControlMessage CreateUdpPacket(string proxyName, byte[] payload, string? host = null, ushort port = 0)
        => new() { Type = MessageType.UdpPacket, ProxyName = proxyName, Payload = payload, Host = host, Port = port };

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }
    }
}
