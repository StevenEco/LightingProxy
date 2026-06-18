namespace LightingProxy.Core.Protocol;

public sealed class ControlMessage
{
    public required MessageType Type { get; init; }

    public string? Token { get; init; }

    public bool Success { get; init; }

    public string? Error { get; init; }

    public string? ProxyName { get; init; }

    public string? Protocol { get; init; }

    public Guid RequestId { get; init; }

    public string? VisitorName { get; init; }

    public string? ServerName { get; init; }

    public string? SecretKey { get; init; }

    public Dictionary<string, string>? Metadata { get; init; }

    public byte[]? Payload { get; init; }

    public string? Host { get; init; }

    public ushort Port { get; init; }
}
