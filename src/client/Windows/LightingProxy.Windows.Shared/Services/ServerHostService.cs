using LightingProxy.Core.Server;
using LightingProxy.Core.Telemetry;
using LightingProxy.Domain.Server;

namespace LightingProxy.Windows.Shared.Services;

public sealed class ServerHostService : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private ProxyServer? _server;
    private Task? _runTask;

    public ProxyConnectionState ConnectionState => _server?.ConnectionState ?? ProxyConnectionState.Stopped;

    public DateTimeOffset? StartedAt => _server?.StartedAt;

    public ProxyServer? Server => _server;

    public string? LastError { get; private set; }

    public event EventHandler? StateChanged;

    public async Task StartAsync(ServerConfig config, CancellationToken cancellationToken = default)
    {
        await StopAsync().ConfigureAwait(false);

        _server = new ProxyServer(config);
        _server.ConnectionStateChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        _runTask = Task.Run(() => _server.StartAsync(linked.Token), linked.Token);
        await Task.Delay(100, linked.Token).ConfigureAwait(false);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task StopAsync()
    {
        if (_server is not null)
        {
            await _server.DisposeAsync().ConfigureAwait(false);
            _server = null;
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
