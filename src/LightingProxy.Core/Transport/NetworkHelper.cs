using System.Net;
using System.Net.Sockets;

namespace LightingProxy.Core.Transport;

public static class NetworkHelper
{
    public static async Task<Socket> ConnectTcpAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        var client = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
        {
            DualMode = true
        };

        try
        {
            await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public static Socket CreateTcpListener(IPEndPoint endpoint)
    {
        var listener = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listener.Bind(endpoint);
        listener.Listen(512);
        return listener;
    }

    public static Socket CreateUdpSocket(IPEndPoint endpoint)
    {
        var socket = new Socket(endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Bind(endpoint);
        return socket;
    }

    public static NetworkStream CreateNetworkStream(Socket socket, bool ownsSocket = true)
        => new(socket, ownsSocket: ownsSocket);

    public static async Task<(IPEndPoint RemoteEndPoint, int ReceivedBytes)> ReceiveUdpAsync(
        Socket socket,
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var remote = new IPEndPoint(
            socket.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any,
            0);
        var result = await socket.ReceiveFromAsync(buffer, remote, cancellationToken).ConfigureAwait(false);
        var sender = result.RemoteEndPoint as IPEndPoint ?? (IPEndPoint)remote;
        return (sender, result.ReceivedBytes);
    }

    public static Task<int> SendUdpAsync(
        Socket socket,
        ReadOnlySpan<byte> payload,
        EndPoint remoteEndPoint,
        CancellationToken cancellationToken = default)
        => socket.SendToAsync(payload.ToArray(), remoteEndPoint, cancellationToken).AsTask();
}
