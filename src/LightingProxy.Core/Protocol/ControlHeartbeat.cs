namespace LightingProxy.Core.Protocol;

/// <summary>
/// 控制连接心跳：客户端按间隔发送 Ping，并在超时未收到 Pong 时终止会话。
/// </summary>
internal sealed class ControlHeartbeat
{
    private readonly int _intervalSeconds;
    private readonly int _timeoutSeconds;
    private readonly Func<CancellationToken, Task> _sendPingAsync;
    private readonly object _sync = new();
    private DateTimeOffset _lastPongUtc = DateTimeOffset.UtcNow;

    public ControlHeartbeat(
        int intervalSeconds,
        int timeoutSeconds,
        Func<CancellationToken, Task> sendPingAsync)
    {
        _intervalSeconds = intervalSeconds;
        _timeoutSeconds = timeoutSeconds;
        _sendPingAsync = sendPingAsync;
    }

    public void NotifyPongReceived()
    {
        lock (_sync)
        {
            _lastPongUtc = DateTimeOffset.UtcNow;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _lastPongUtc = DateTimeOffset.UtcNow;
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_intervalSeconds <= 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_intervalSeconds));

        while (!cancellationToken.IsCancellationRequested)
        {
            EnsureNotTimedOut(cancellationToken);
            await _sendPingAsync(cancellationToken).ConfigureAwait(false);

            if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                break;
            }
        }
    }

    private void EnsureNotTimedOut(CancellationToken cancellationToken)
    {
        DateTimeOffset lastPong;
        lock (_sync)
        {
            lastPong = _lastPongUtc;
        }

        if (DateTimeOffset.UtcNow - lastPong > TimeSpan.FromSeconds(_timeoutSeconds))
        {
            throw new TimeoutException(
                $"Heartbeat timeout: no Pong received within {_timeoutSeconds} seconds.");
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
