using System.Net;
using System.Net.Sockets;
using System.Text;
using LightingProxy.Core.Abstractions;
using LightingProxy.Core.Transport;
using LightingProxy.Domain.Client.Proxies;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Core.Proxies;

public sealed class Socks5ProxyHandler : IProxyHandler
{
    public LightingProxy.Domain.Enums.ProtocolType Protocol => LightingProxy.Domain.Enums.ProtocolType.Socks5;

    public Dictionary<string, string> BuildMetadata(ProxyConfigBase config)
        => ProxyMetadataBuilder.Build(config);

    public Task StartServerAsync(ProxyDefinition definition, IServerProxyContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task HandleClientWorkConnectionAsync(ProxyDefinition definition, Stream workStream, IClientProxyContext context, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[512];
        var read = await workStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        var target = ParseSocks5ConnectTarget(buffer.AsSpan(0, read));

        var outbound = await NetworkHelper.ConnectTcpAsync(target.Host, target.Port, cancellationToken).ConfigureAwait(false);
        await using var outboundStream = NetworkHelper.CreateNetworkStream(outbound, ownsSocket: true);

        var response = new byte[] { 0x05, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        await workStream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
        await StreamRelay.RelayBidirectionalAsync(workStream, outboundStream, cancellationToken).ConfigureAwait(false);
    }

    public static async Task RunLocalSocks5ServerAsync(
        IPEndPoint endpoint,
        string? username,
        string? password,
        Func<string, int, CancellationToken, Task<Stream>> tunnelFactory,
        CancellationToken cancellationToken)
    {
        using var listener = NetworkHelper.CreateTcpListener(endpoint);

        while (!cancellationToken.IsCancellationRequested)
        {
            var socket = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
            _ = HandleSocksClientAsync(socket, username, password, tunnelFactory, cancellationToken);
        }
    }

    private static async Task HandleSocksClientAsync(
        Socket socket,
        string? username,
        string? password,
        Func<string, int, CancellationToken, Task<Stream>> tunnelFactory,
        CancellationToken cancellationToken)
    {
        await using var stream = NetworkHelper.CreateNetworkStream(socket);
        var greeting = new byte[2];
        await stream.ReadExactlyAsync(greeting, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(new byte[] { 0x05, username is null ? (byte)0x00 : (byte)0x02 }, cancellationToken).ConfigureAwait(false);

        if (username is not null)
        {
            var authHeader = new byte[2];
            await stream.ReadExactlyAsync(authHeader, cancellationToken).ConfigureAwait(false);
            var userLength = authHeader[1];
            var userBytes = new byte[userLength];
            await stream.ReadExactlyAsync(userBytes, cancellationToken).ConfigureAwait(false);
            var passLengthBuffer = new byte[1];
            await stream.ReadExactlyAsync(passLengthBuffer, cancellationToken).ConfigureAwait(false);
            var passBytes = new byte[passLengthBuffer[0]];
            await stream.ReadExactlyAsync(passBytes, cancellationToken).ConfigureAwait(false);

            var user = Encoding.ASCII.GetString(userBytes);
            var pass = Encoding.ASCII.GetString(passBytes);
            var ok = user == username && pass == password;
            await stream.WriteAsync(new byte[] { 0x01, ok ? (byte)0x00 : (byte)0x01 }, cancellationToken).ConfigureAwait(false);
            if (!ok)
            {
                return;
            }
        }

        var request = new byte[4];
        await stream.ReadExactlyAsync(request, cancellationToken).ConfigureAwait(false);
        var target = await ReadSocksTargetAsync(stream, request[3], cancellationToken).ConfigureAwait(false);
        await using var tunnel = await tunnelFactory(target.Host, target.Port, cancellationToken).ConfigureAwait(false);

        var success = new byte[] { 0x05, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        await stream.WriteAsync(success, cancellationToken).ConfigureAwait(false);
        await StreamRelay.RelayBidirectionalAsync(stream, tunnel, cancellationToken).ConfigureAwait(false);
    }

    private static (string Host, int Port) ParseSocks5ConnectTarget(ReadOnlySpan<byte> request)
    {
        if (request.Length < 4 || request[0] != 0x05 || request[1] != 0x01)
        {
            throw new InvalidOperationException("Invalid SOCKS5 connect request.");
        }

        return request[3] switch
        {
            0x01 => ParseIpv4(request),
            0x03 => ParseDomain(request),
            _ => throw new NotSupportedException("Unsupported SOCKS5 address type.")
        };
    }

    private static async Task<(string Host, int Port)> ReadSocksTargetAsync(Stream stream, byte addressType, CancellationToken cancellationToken)
    {
        return addressType switch
        {
            0x01 => await ReadIpv4TargetAsync(stream, cancellationToken).ConfigureAwait(false),
            0x03 => await ReadDomainTargetAsync(stream, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException("Unsupported SOCKS5 address type.")
        };
    }

    private static (string Host, int Port) ParseIpv4(ReadOnlySpan<byte> request)
    {
        var host = new IPAddress(request.Slice(4, 4));
        var port = (request[8] << 8) | request[9];
        return (host.ToString(), port);
    }

    private static async Task<(string Host, int Port)> ReadIpv4TargetAsync(Stream stream, CancellationToken cancellationToken)
    {
        var address = new byte[4];
        var portBytes = new byte[2];
        await stream.ReadExactlyAsync(address, cancellationToken).ConfigureAwait(false);
        await stream.ReadExactlyAsync(portBytes, cancellationToken).ConfigureAwait(false);
        var host = new IPAddress(address);
        var port = (portBytes[0] << 8) | portBytes[1];
        return (host.ToString(), port);
    }

    private static (string Host, int Port) ParseDomain(ReadOnlySpan<byte> request)
    {
        var length = request[4];
        var host = Encoding.ASCII.GetString(request.Slice(5, length));
        var portIndex = 5 + length;
        var port = (request[portIndex] << 8) | request[portIndex + 1];
        return (host, port);
    }

    private static async Task<(string Host, int Port)> ReadDomainTargetAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[1];
        await stream.ReadExactlyAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
        var hostBytes = new byte[lengthBuffer[0]];
        var portBytes = new byte[2];
        await stream.ReadExactlyAsync(hostBytes, cancellationToken).ConfigureAwait(false);
        await stream.ReadExactlyAsync(portBytes, cancellationToken).ConfigureAwait(false);
        var host = Encoding.ASCII.GetString(hostBytes);
        var port = (portBytes[0] << 8) | portBytes[1];
        return (host, port);
    }
}
