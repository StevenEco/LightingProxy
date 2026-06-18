using System.Collections.Concurrent;

namespace LightingProxy.Core.Server;

public sealed class WorkConnectionBroker
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<Stream>> _pending = new();

    public Task<Stream> WaitForWorkConnectionAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var tcs = _pending.GetOrAdd(requestId, _ => new TaskCompletionSource<Stream>(TaskCreationOptions.RunContinuationsAsynchronously));
        return tcs.Task.WaitAsync(cancellationToken);
    }

    public void Complete(Guid requestId, Stream stream)
    {
        if (_pending.TryRemove(requestId, out var tcs))
        {
            tcs.TrySetResult(stream);
        }
        else
        {
            stream.Dispose();
        }
    }

    public bool HasPending(Guid requestId) => _pending.ContainsKey(requestId);

    public void Cancel(Guid requestId, Exception? exception = null)
    {
        if (_pending.TryRemove(requestId, out var tcs))
        {
            tcs.TrySetException(exception ?? new InvalidOperationException("Work connection request cancelled."));
        }
    }
}
