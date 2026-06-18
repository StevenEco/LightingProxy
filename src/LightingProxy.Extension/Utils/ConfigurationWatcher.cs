namespace LightingProxy.Extension.Utils;

public sealed class ConfigurationWatcher<T> : IDisposable
{
    private readonly string _filePath;
    private readonly Func<string, T> _loader;
    private readonly Action<T> _onChanged;
    private readonly object _sync = new();
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _debounceTimer;
    private readonly int _debounceMilliseconds;
    private bool _disposed;

    public ConfigurationWatcher(string filePath, Func<string, T> loader, Action<T> onChanged, int debounceMilliseconds = 300)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(onChanged);

        if (debounceMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(debounceMilliseconds), "Debounce interval cannot be negative.");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Configuration file was not found.", filePath);
        }

        _filePath = Path.GetFullPath(filePath);
        _loader = loader;
        _onChanged = onChanged;
        _debounceMilliseconds = debounceMilliseconds;

        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Configuration file path must include a directory.");

        _watcher = new FileSystemWatcher(directory, Path.GetFileName(_filePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.EnableRaisingEvents = true;

        _debounceTimer = new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public event Action<Exception>? ReloadFailed;

    public void Reload()
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            try
            {
                var config = _loader(_filePath);
                _onChanged(config);
            }
            catch (Exception ex)
            {
                ReloadFailed?.Invoke(ex);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _debounceTimer.Dispose();
        _watcher.Dispose();
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!IsTargetFile(e.FullPath))
        {
            return;
        }

        ScheduleReload();
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsTargetFile(e.FullPath))
        {
            return;
        }

        ScheduleReload();
    }

    private void ScheduleReload()
    {
        _debounceTimer.Change(_debounceMilliseconds, Timeout.Infinite);
    }

    private bool IsTargetFile(string fullPath)
    {
        return string.Equals(Path.GetFullPath(fullPath), _filePath, StringComparison.OrdinalIgnoreCase);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
