using System.Diagnostics;
using System.IO;
using System.Windows;
using Nepply.Models;
using Nepply.Modules.Desktop;
using DesktopIconModel = Nepply.Models.DesktopIcon;

namespace Nepply.Services;

public class DesktopIconService : IDisposable, IDesktopCatalog, IDesktopActions
{


    private readonly DesktopVisibilityLease _desktopVisibility = DesktopVisibilityLease.Create();
    private readonly GuardedDesktopVisibility? _guardedVisibility;
    private bool _ownsDesktopVisibility;
    private readonly string? _scanExecutable;
    private readonly Func<DesktopScanOptions, CancellationToken, Task<List<DesktopIconModel>>>? _scanner;
    private readonly Func<DesktopScanOptions, string[]?, CancellationToken, Task<List<DesktopIconModel>>>? _rawScanner;
    private readonly Func<string[]> _desktopRoots = DesktopEntryReader.GetDesktopRoots;
    private List<DesktopIconModel>? _rawSnapshot;
    private string[]? _rawRoots;
    private int _configurationVersion;
    private readonly DesktopChangeBatch _pendingChanges = new();
    private readonly CancellationTokenSource _scanShutdown = new();
    public DesktopIconService(string? scanExecutable = null)
    {
        _scanExecutable = scanExecutable;
        _guardedVisibility = new GuardedDesktopVisibility(new ExplorerDesktopVisibility(), _desktopVisibility, DesktopGuardianDeployment.StartAsync);
    }
    internal DesktopIconService(Func<CancellationToken, Task<List<DesktopIconModel>>> scanner) => _scanner = (_, token) => scanner(token);
    internal DesktopIconService(Func<DesktopScanOptions, CancellationToken, Task<List<DesktopIconModel>>> scanner) => _scanner = scanner;
    internal DesktopIconService(DesktopVisibilityLease visibility) => _desktopVisibility = visibility;
    internal DesktopIconService(Func<DesktopScanOptions, string[]?, CancellationToken, Task<List<DesktopIconModel>>> scanner, Func<string[]> roots)
    { _rawScanner = scanner; _desktopRoots = roots; }

    private readonly DesktopCatalogStore _catalog = new();
    public IReadOnlyList<DesktopIconModel> GetIcons() => _catalog.Read();
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly object _scanRequests = new();
    private Task? _scanTask;
    private int _scanGeneration;
    private volatile bool _disposed;
    private DesktopScanOptions _options = new();
    internal void Configure(DesktopScanOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (_scanRequests)
        {
            if (_disposed || _options == options) return;
            Volatile.Write(ref _options, options);
            _configurationVersion++;
            _rawSnapshot = null;
        }
    }
    internal void RestorePreviewCatalog(DesktopScanOptions options, IReadOnlyList<DesktopIconModel> snapshot, bool hasSnapshot)
    {
        bool changed;
        lock (_scanRequests)
        {
            if (_disposed) return;
            Volatile.Write(ref _options, options);
            _configurationVersion++;
            _rawSnapshot = null;
            changed = _catalog.Reconcile(snapshot, DesktopSnapshot.Reconcile);
            HasScanned = hasSnapshot;
        }
        SetScanState(new(false, hasSnapshot));
        if (changed) IconsChanged?.Invoke();
    }
    public bool ShowHiddenFiles { get => Volatile.Read(ref _options).ShowHiddenFiles; set { lock (_scanRequests) Configure(_options with { ShowHiddenFiles = value }); } }
    public bool ShowSystemIcons { get => Volatile.Read(ref _options).ShowSystemIcons; set { lock (_scanRequests) Configure(_options with { ShowSystemIcons = value }); } }
    public bool MergeEquivalentShortcuts { get => Volatile.Read(ref _options).MergeEquivalentShortcuts; set { lock (_scanRequests) Configure(_options with { MergeEquivalentShortcuts = value }); } }
    public bool HasScanned { get; private set; }
    private DesktopScanState _scanState = new(false, false);
    private string? _scanFailureDiagnostic;
    internal string? ScanFailureDiagnostic => Volatile.Read(ref _scanFailureDiagnostic);
    public DesktopScanState ScanState => Volatile.Read(ref _scanState);
    public event Action? ScanStateChanged;
    private void SetScanState(DesktopScanState state)
    {
        if (_disposed) return;
        Volatile.Write(ref _scanState, state);
        ScanStateChanged?.Invoke();
    }
    public event Action? IconsChanged;

    public void ScanDesktopIcons()
    {
        _ = ScanDesktopIconsAsync();
    }
    void IDesktopActions.Refresh() => ScanDesktopIcons();

    public Task ScanDesktopIconsAsync() => RequestScan(null);
    internal Task ScanDesktopChangesAsync(IEnumerable<string>? paths) => RequestScan(paths);
    private Task RequestScan(IEnumerable<string>? paths)
    {
        lock (_scanRequests)
        {
            if (_disposed) return Task.CompletedTask;
            _pendingChanges.Add(paths);
            if (!_pendingChanges.HasChanges) return _scanTask ?? Task.CompletedTask;
            if (_scanTask == null || _scanTask.IsCompleted)
                _scanTask = Task.Run(ProcessScanRequestsAsync);
            return _scanTask;
        }
    }
    private async Task ProcessScanRequestsAsync()
    {
        while (true)
        {
            string[]? paths;
            lock (_scanRequests)
            {
                if (_disposed || !_pendingChanges.HasChanges) { _scanTask = null; return; }
                paths = _pendingChanges.Take();
            }
            await ScanOnceAsync(paths).ConfigureAwait(false);
        }
    }
    private async Task ScanOnceAsync(string[]? paths)
    {
        int generation = Interlocked.Increment(ref _scanGeneration);
        await _scanGate.WaitAsync();
        try
        {
            if (_disposed || generation != Volatile.Read(ref _scanGeneration)) return;
            DesktopScanOptions options;
            List<DesktopIconModel>? previousRaw;
            string[]? previousRoots;
            int configurationVersion;
            lock (_scanRequests)
            {
                options = _options; previousRaw = _rawSnapshot; previousRoots = _rawRoots; configurationVersion = _configurationVersion;
            }
            var roots = _desktopRoots();
            if (previousRaw == null || previousRoots == null || !previousRoots.SequenceEqual(roots, StringComparer.OrdinalIgnoreCase)
                || _scanner != null || paths?.Any(p => !DesktopChangeBatch.IsDirectChild(p, roots)) == true)
                paths = null;
            Volatile.Write(ref _scanFailureDiagnostic, null);
            SetScanState(new(true, HasScanned));
            var incoming = _rawScanner != null ? await _rawScanner(options, paths, _scanShutdown.Token)
                : _scanner != null ? await _scanner(options, _scanShutdown.Token)
                : _scanExecutable != null
                    ? await DesktopScanProcess.ReadAsync(_scanExecutable, options.ShowHiddenFiles, options.ShowSystemIcons, _scanShutdown.Token, options.MergeEquivalentShortcuts, paths, raw: true)
                    : await DesktopEntryReader.ReadNativeAsync(options, paths);
            var raw = DesktopRawCatalog.BoundImages(paths == null ? incoming : DesktopRawCatalog.Merge(previousRaw!, incoming, paths));
            var results = _scanner != null ? incoming : DesktopRawCatalog.Group(raw, options.MergeEquivalentShortcuts);

            if (_disposed || generation != Volatile.Read(ref _scanGeneration)) return;
            bool changed;
            lock (_scanRequests)
            {
                if (_disposed || generation != Volatile.Read(ref _scanGeneration)) return;
                if (configurationVersion != _configurationVersion)
                {
                    // A settings preview was replaced/cancelled while enumeration ran.
                    // Never publish that obsolete catalog; reconcile the latest options.
                    _pendingChanges.Full();
                    return;
                }
                changed = _catalog.Reconcile(results.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase), DesktopSnapshot.Reconcile);
                _rawSnapshot = _scanner == null ? raw : null;
                _rawRoots = roots;
                HasScanned = true;
            }
            SetScanState(new(false, true));
            if (changed) IconsChanged?.Invoke();
        }
        catch (OperationCanceledException) when (_scanShutdown.IsCancellationRequested) { }
        catch (Exception ex)
        {
            lock (_scanRequests) _rawSnapshot = null; // Next request repairs the complete baseline.
            Volatile.Write(ref _scanFailureDiagnostic, AppLogger.LimitText(ex.ToString()));
            AppLogger.Error(ex, "Desktop scan");
            SetScanState(new(false, HasScanned, HelperFailure.IsPolicyBlocked(ex) ? HelperFailure.PolicyMessageKey
                : HasScanned ? "DesktopRefreshError" : "DesktopReadError"));
        }
        finally { _scanGate.Release(); }
    }


    #region Context Menu Actions

    private readonly ShellLaunchQueue _launches = new();
    public Task<UserMessage?> LaunchIconAsync(DesktopIconModel icon) => _launches.Run(icon.Path, () => LaunchIcon(icon));
    private static UserMessage? LaunchIcon(DesktopIconModel icon)
    {
        try
        {
            using var process = Process.Start(CreateLaunchInfo(icon));
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // The user cancelled Windows elevation/consent; not a launch failure.
            return null;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Launch desktop item");
            return UserMessage.FromKey("DesktopLaunchFailed", icon.Name, UserMessage.FromKey(LaunchFailureKey(ex)));
        }
        return null;
    }
    internal static ProcessStartInfo CreateLaunchInfo(DesktopIconModel icon)
    {
        if (!icon.IsSystemIcon) return new ProcessStartInfo(icon.Path) { UseShellExecute = true };
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!Path.IsPathFullyQualified(windows)) throw new IOException("Windows directory is unavailable.");
        var start = new ProcessStartInfo(Path.Combine(windows, "explorer.exe")) { UseShellExecute = true };
        start.ArgumentList.Add(icon.Path);
        return start;
    }
    internal static string LaunchFailureKey(Exception error) => error switch
    {
        System.ComponentModel.Win32Exception { NativeErrorCode: 2 or 3 } => "DesktopLaunchMissing",
        System.ComponentModel.Win32Exception { NativeErrorCode: 5 } or UnauthorizedAccessException => "DesktopLaunchDenied",
        System.ComponentModel.Win32Exception { NativeErrorCode: 1155 } => "DesktopLaunchAssociation",
        _ => "DesktopLaunchError"
    };

    /// <summary>
    /// Show the native Windows Shell context menu for a file path.
    /// </summary>
    public UserMessage? ShowShellContextMenu(DesktopIconModel icon, IntPtr hwnd, System.Windows.Point? screenAnchor = null)
        => Helpers.ShellContextMenu.Show(icon, hwnd, screenAnchor);

    #endregion

    #region Desktop Icon Visibility

    internal UserMessage? HideDesktopIcons() => ToggleDesktopIcons(hide: true);
    internal UserMessage? ShowDesktopIcons() => ToggleDesktopIcons(hide: false);

    public async Task<UserMessage?> SetDesktopIconsHiddenAsync(bool hide)
    {
        if (_disposed) return UserMessage.FromKey("DesktopClosingNotice");
        if (_guardedVisibility is null) return ToggleDesktopIcons(hide);
        bool success = hide ? await _guardedVisibility.HideAsync().ConfigureAwait(false)
            : await _guardedVisibility.RestoreAsync().ConfigureAwait(false);
        _ownsDesktopVisibility = _desktopVisibility.OwnsChange;
        if (success) return null;
        return UserMessage.FromKey(hide ? "DesktopGuardHideError" : "DesktopGuardRestoreError");
    }

    private UserMessage? ToggleDesktopIcons(bool hide)
    {
        try
        {
            if (hide)
            {
                if (_desktopVisibility.Hide() || _desktopVisibility.IsCurrentlyHidden) return null;
            }
            else if (!_ownsDesktopVisibility || _desktopVisibility.Restore()) return null;
        }
        catch (Exception ex) { AppLogger.Error(ex, "Failed to toggle desktop icons visibility"); }
        finally
        {
            // Explorer may change visibility and then fail. Keep ownership of the
            // durable recovery intent even when the native operation throws.
            _ownsDesktopVisibility = _desktopVisibility.OwnsChange;
        }
        return UserMessage.FromKey(hide ? "DesktopHideError" : "DesktopRestoreError");
    }

    public bool AreDesktopIconsHidden => _ownsDesktopVisibility;

    public void Dispose()
    {
        lock (_scanRequests)
        {
            if (_disposed) return;
            _disposed = true;
            _rawSnapshot = null;
            _rawRoots = null;
            _pendingChanges.Take();
        }
        try { _scanShutdown.Cancel(); }
        finally
        {
            // A failing cancellation callback must not skip restoring Desktop.
            Interlocked.Increment(ref _scanGeneration);
            IconsChanged = null;
            ScanStateChanged = null;
            if (_guardedVisibility is not null)
            {
                // Every continuation in the coordinator is independent of the WPF Dispatcher.
                // Stop/cancel pending hides before synchronous final application teardown.
                if (!_guardedVisibility.RestoreAsync(stopping: true).GetAwaiter().GetResult())
                    AppLogger.Error(new IOException(_guardedVisibility.LastError ?? "Desktop recovery failed."), "Restore Desktop on shutdown");
            }
            else if (_ownsDesktopVisibility) ShowDesktopIcons();
        }
    }

    #endregion
}
