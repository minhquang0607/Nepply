using System.IO;
namespace Nepply.Services;

public sealed class DesktopWatcherService : IDisposable
{
    private sealed record Subscription(FileSystemWatcher Watcher, DateTime CreatedUtc);
    private readonly Dictionary<string, Subscription> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<FileSystemWatcher> _failed = [];
    private readonly object _gate = new();
    private readonly Timer _timer;
    private readonly Timer _recovery;
    private readonly BoundedDebounce _debounce = new();
    private readonly Func<IEnumerable<string>> _paths;
    private readonly Action<string[]?> _scan;
    private readonly Modules.Desktop.DesktopChangeBatch _changes=new();
    private readonly int _recheckIntervalMs;
    private readonly int _repairIntervalMs;
    private readonly Func<long> _clock;
    private readonly Func<string, DateTime?> _probe;
    private readonly Func<string, FileSystemWatcher> _createWatcher;
    private Task _worker = Task.CompletedTask;
    private Task? _cleanup;
    private bool _reconciling, _reconcileAgain;
    private int _watchedCount;
    private long _lastRefresh;
    private bool _initialized;
    private bool _disposed;
    public DesktopWatcherService(DesktopIconService icons) : this(icons.ScanDesktopIcons, () =>
        [Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)],
        scanChanges:paths=> { _=icons.ScanDesktopChangesAsync(paths); }) { }

    internal DesktopWatcherService(Action scan, Func<IEnumerable<string>> paths, int recheckIntervalMs = 30000,
        int repairIntervalMs = 300000, Func<long>? clock = null,
        Func<string, DateTime?>? probe = null, Func<string, FileSystemWatcher>? createWatcher = null,Action<string[]?>? scanChanges = null)
    {
        _recheckIntervalMs = Math.Max(100,recheckIntervalMs);
        _repairIntervalMs = Math.Max(_recheckIntervalMs, repairIntervalMs);
        _clock = clock ?? (() => Environment.TickCount64);
        _lastRefresh = _clock();
        _scan = scanChanges ?? (_=>scan()); _paths = paths;
        _probe = probe ?? (path => Directory.Exists(path) ? Directory.GetCreationTimeUtc(path) : null);
        _createWatcher = createWatcher ?? (path => new FileSystemWatcher(path)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Attributes,
            InternalBufferSize = 32768
        });
        _timer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        _recovery = new Timer(state => { _ = ReconcileAsync(); }, null, Timeout.Infinite, Timeout.Infinite);
        _recovery.Change(_recheckIntervalMs, _recheckIntervalMs);
        _ = ReconcileAsync();
    }
    private void Flush()
    {
        lock (_gate)
        {
            if (_disposed) return;
            // Timer.Change cannot retract a callback already queued. Honor the
            // newest deadline and ignore a duplicate callback after reset.
            var remaining=_debounce.Remaining(Environment.TickCount64);
            if(remaining==null) return;
            if(remaining>0) { _timer.Change(remaining.Value,Timeout.Infinite); return; }
            _debounce.Reset();
            var paths=_changes.Take();
            if(paths==null) _lastRefresh = _clock();
            try { _scan(paths); } catch (Exception ex) { AppLogger.Error(ex,"Request Desktop refresh"); }
        }
    }
    internal int WatchedCount => Volatile.Read(ref _watchedCount);
    internal Task Completion { get { lock (_gate) return _cleanup ?? _worker; } }
    public void RequestRefresh() => RequestChanges(null);
    internal void RequestChanges(string[]? paths)
    {
        lock(_gate)
        {
            if(_disposed) return;
            _changes.Add(paths);
            if(_changes.HasChanges) Schedule();
        }
    }
    // Only this single-flight worker owns the subscription dictionary. No filesystem
    // operation or native watcher disposal runs under the UI-facing state lock.
    internal Task ReconcileAsync()
    {
        lock (_gate)
        {
            if (_disposed) return _cleanup ?? _worker;
            _reconcileAgain = true;
            if (!_reconciling)
            {
                _reconciling = true;
                _worker = Task.Run(ReconcileLoop);
            }
            return _worker;
        }
    }
    private void ReconcileLoop()
    {
        while (true)
        {
            HashSet<FileSystemWatcher> failed;
            lock (_gate)
            {
                if (_disposed || !_reconcileAgain) { _reconciling = false; return; }
                _reconcileAgain = false;
                failed = new(_failed);
                _failed.Clear();
            }
            try
            {
                bool changed = !_initialized;
                _initialized = true;
                var paths = _paths().Where(p => !string.IsNullOrWhiteSpace(p)).Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var available = paths.ToDictionary(path => path, path => _probe(path), StringComparer.OrdinalIgnoreCase);
                if (Volatile.Read(ref _disposed)) return;
                foreach (var entry in _watchers.ToArray())
                {
                    if (!available.TryGetValue(entry.Key, out var created) || created == null
                        || failed.Contains(entry.Value.Watcher) || created != entry.Value.CreatedUtc)
                    {
                        _watchers.Remove(entry.Key);
                        changed = true;
                        Release(entry.Value.Watcher);
                    }
                }
                foreach (var path in paths)
                {
                    if (Volatile.Read(ref _disposed)) return;
                    if (_watchers.ContainsKey(path) || available[path] == null) continue;
                    FileSystemWatcher? watcher = null;
                    try
                    {
                        var created = available[path]!.Value;
                        watcher = _createWatcher(path);
                        watcher.Created += Changed; watcher.Deleted += Changed; watcher.Renamed += Changed; watcher.Changed += Changed;
                        watcher.Error += Error; watcher.EnableRaisingEvents = true;
                        _watchers.Add(path,new(watcher,created));
                        changed = true;
                    }
                    catch (Exception ex)
                    {
                        if (watcher != null) Release(watcher);
                        AppLogger.Error(ex,"Start Desktop folder watcher");
                    }
                }
                // Healthy subscriptions need no content scan. Keep a slower repair
                // backstop for lost notifications; recent event scans reset its age.
                lock (_gate)
                {
                    if (_disposed) return;
                    Volatile.Write(ref _watchedCount, _watchers.Count);
                    if (changed || _clock() - _lastRefresh >= _repairIntervalMs) { _changes.Full(); Schedule(); }
                }
            }
            catch (Exception ex)
            {
                // A failed probe must not consume the repair intent for a broken subscription.
                lock (_gate) { if (!_disposed) _failed.UnionWith(failed); }
                AppLogger.Error(ex,"Recover Desktop watchers");
            }
        }
    }
    private void Schedule() => _timer.Change(_debounce.Signal(Environment.TickCount64),Timeout.Infinite);
    private void Changed(object sender, FileSystemEventArgs args)
    {
        RequestChanges(args is RenamedEventArgs renamed ? [renamed.OldFullPath,renamed.FullPath] : [args.FullPath]);
    }
    private void Error(object sender, ErrorEventArgs args)
    {
        lock (_gate)
        {
            if (_disposed) return;
            AppLogger.Error(args.GetException(),"Desktop watcher");
            if (sender is FileSystemWatcher watcher) _failed.Add(watcher);
            _changes.Full();
            Schedule();
            _recovery.Change(Math.Min(1000,_recheckIntervalMs),_recheckIntervalMs);
        }
    }
    private void Release(FileSystemWatcher watcher)
    {
        watcher.Created -= Changed; watcher.Deleted -= Changed; watcher.Renamed -= Changed; watcher.Changed -= Changed; watcher.Error -= Error;
        try { watcher.Dispose(); }
        catch (Exception ex) { AppLogger.Error(ex,"Release Desktop watcher"); }
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return; _disposed = true;
            _timer.Dispose(); _recovery.Dispose();
            Volatile.Write(ref _watchedCount, 0);
            _failed.Clear();
            // Do not wait for an unavailable UNC path from Window.Cleanup. A watcher
            // created after shutdown is still owned by this worker and released here.
            _cleanup = _worker.ContinueWith(_ =>
            {
                foreach (var entry in _watchers.Values) Release(entry.Watcher);
                _watchers.Clear();
            }, TaskScheduler.Default);
        }
    }
}
