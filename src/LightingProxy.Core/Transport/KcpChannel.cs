using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace LightingProxy.Core.Transport;

public sealed class KcpChannel : IDisposable
{
    private readonly Socket _socket;
    private readonly ConcurrentDictionary<uint, byte[]> _pending = new();
    private uint _nextSequence;
    private readonly CancellationTokenSource _cts = new();

    public KcpChannel(IPEndPoint localEndpoint)
    {
        _socket = NetworkHelper.CreateUdpSocket(localEndpoint);
    }

    public int LocalPort => ((IPEndPoint)_socket.LocalEndPoint!).Port;

    public async Task SendAsync(IPEndPoint remote, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        var sequence = Interlocked.Increment(ref _nextSequence);
        var packet = new byte[12 + payload.Length];
        BitConverter.TryWriteBytes(packet.AsSpan(0, 4), sequence);
        BitConverter.TryWriteBytes(packet.AsSpan(4, 4), 0u);
        BitConverter.TryWriteBytes(packet.AsSpan(8, 4), (uint)payload.Length);
        payload.CopyTo(packet.AsMemory(12));

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await NetworkHelper.SendUdpAsync(_socket, packet, remote, cancellationToken).ConfigureAwait(false);
            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<(IPEndPoint Remote, byte[] Payload)?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[64 * 1024];

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await NetworkHelper.ReceiveUdpAsync(_socket, buffer, cancellationToken).ConfigureAwait(false);
            if (result.ReceivedBytes < 12)
            {
                continue;
            }

            var sequence = BitConverter.ToUInt32(buffer, 0);
            var length = BitConverter.ToUInt32(buffer, 8);
            if (result.ReceivedBytes < 12 + length)
            {
                continue;
            }

            if (!_pending.TryAdd(sequence, buffer.AsSpan(12, (int)length).ToArray()))
            {
                continue;
            }

            return (result.RemoteEndPoint, _pending[sequence]);
        }

        return null;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _socket.Dispose();
    }
}
