namespace LightingProxy.Core.Transport;

using LightingProxy.Core.Telemetry;

public static class StreamRelay
{
    public static async Task RelayBidirectionalAsync(Stream first, Stream second, CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var firstToSecond = PumpAsync(first, second, linked.Token, countAsOutbound: true);
        var secondToFirst = PumpAsync(second, first, linked.Token, countAsOutbound: false);

        var completed = await Task.WhenAny(firstToSecond, secondToFirst).ConfigureAwait(false);
        await linked.CancelAsync().ConfigureAwait(false);

        try
        {
            await Task.WhenAll(firstToSecond, secondToFirst).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            // Expected when one relay direction finishes.
        }
    }

    public static async Task PumpAsync(Stream source, Stream destination, CancellationToken cancellationToken = default, bool countAsOutbound = false)
    {
        var buffer = new byte[32 * 1024];
        var meter = TrafficScope.Current;

        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (meter is not null)
            {
                if (countAsOutbound)
                {
                    meter.RecordOutbound(read);
                }
                else
                {
                    meter.RecordInbound(read);
                }
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
