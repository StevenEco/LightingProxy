using LightingProxy.Core.Client;
using LightingProxy.Core.Telemetry;
using LightingProxy.Domain.Client;

namespace LightingProxy.Windows.Shared.Services;

public sealed class ClientHostService : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private ProxyClient? _client;
    private Task? _runTask;

    public ProxyConnectionState ConnectionState => _client?.ConnectionState ?? ProxyConnectionState.Stopped;

    public DateTimeOffset? ConnectedAt => _client?.ConnectedAt;

    public DateTimeOffset? LastHeartbeatUtc => _client?.LastHeartbeatUtc;

    public ProxyRuntimeTracker? Runtime => _client?.Runtime;

    public string? LastError { get; private set; }

    public event EventHandler? StateChanged;

    public async Task StartAsync(ClientConfig config, CancellationToken cancellationToken = default)
    {
        await StopAsync().ConfigureAwait(false);

        _client = new ProxyClient(config);
        _client.ConnectionStateChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        _runTask = _client.StartAsync(linked.Token);
        await _client.ReadyTask.WaitAsync(linked.Token).ConfigureAwait(false);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task StopAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }

        if (_runTask is not null)
        {
            try
            {
                await _runTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException)
            {
                LastError = ex.Message;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }

            _runTask = null;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        await StopAsync().ConfigureAwait(false);
        _cts.Dispose();
    }
}
