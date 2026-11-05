namespace Nepply.Services;

/// <summary>Serializes hide/restore with guardian readiness. No UI or deployment ownership.</summary>
internal sealed class GuardedDesktopVisibility
{
    private readonly IDesktopVisibility _desktop;
    private readonly DesktopVisibilityLease _lease;
    private readonly Func<CancellationToken, Task<IDesktopGuardianSession>> _start;
    private readonly Action<Exception?, string> _log;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IDesktopGuardianSession? _guardian;
    private bool _hideAttempted, _stopping;
    internal Task RecoveryObservation { get; private set; } = Task.CompletedTask;
    internal string? LastError { get; private set; }
    /// <summary>A guardian died while Desktop was hidden. True when Desktop came back. Raised off the UI thread.</summary>
    internal event Action<bool>? GuardianExited;

    internal GuardedDesktopVisibility(IDesktopVisibility desktop, DesktopVisibilityLease lease, Func<Task<IDesktopGuardianSession>> start, Action<Exception?, string>? log = null)
        : this(desktop, lease, _ => start(), log) { }

    internal GuardedDesktopVisibility(IDesktopVisibility desktop, DesktopVisibilityLease lease, Func<CancellationToken, Task<IDesktopGuardianSession>> start, Action<Exception?, string>? log = null)
    { _desktop = desktop; _lease = lease; _start = start; _log = log ?? AppLogger.Error; }

    internal async Task<bool> HideAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_stopping || _shutdown.IsCancellationRequested) return false;
            LastError = null;
            if (_guardian is not null)
            {
                if (_hideAttempted && !_guardian.Exited.IsCompleted && _desktop.IsVisible() == false) return true;
                if (!await RestoreCoreAsync().ConfigureAwait(false)) return false;
            }
            bool? visible = _desktop.IsVisible();
            if (visible == false) return true; // User-owned hidden Desktop, no helper and no lease.
            if (visible != true) return Fail("Desktop unavailable.");
            _guardian = await _start(_shutdown.Token).ConfigureAwait(false);
            var guardian = _guardian;
            RecoveryObservation = ObserveExitAsync(guardian);
            _shutdown.Token.ThrowIfCancellationRequested();
            if (guardian.Exited.IsCompleted || !await guardian.ArmAsync(_shutdown.Token).ConfigureAwait(false))
            {
                string reason = "Guardian did not arm" + (guardian.Exited.IsCompleted ? $" ({ExitDetail(guardian)})." : ".");
                await RestoreCoreAsync().ConfigureAwait(false);
                return Fail(reason);
            }
            _hideAttempted = true; // Native hide may change Explorer and then throw.
            if (!_lease.Hide() || guardian.Exited.IsCompleted)
            {
                await RestoreCoreAsync().ConfigureAwait(false);
                return Fail("Desktop hide did not complete with a live guardian.");
            }
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            // Stopping cancels a pending startup on purpose; that is not a failure.
            if (ex is not OperationCanceledException || !_shutdown.IsCancellationRequested) _log(ex, "Hide Desktop icons");
            try { await RestoreCoreAsync().ConfigureAwait(false); }
            catch (Exception recovery)
            {
                LastError += "; " + recovery.Message;
                _log(recovery, "Restore Desktop after a failed hide");
            }
            return false;
        }
        finally { _gate.Release(); }
    }

    internal async Task<bool> RestoreAsync(bool stopping = false)
    {
        if (stopping) _shutdown.Cancel(); // Interrupt startup before waiting for its operation gate.
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _stopping |= stopping;
            return await RestoreCoreAsync().ConfigureAwait(false);
        }
        catch (Exception ex) { return Fail(ex.Message, ex); }
        finally { _gate.Release(); }
    }

    private bool RestoreLocal()
    {
        if (!_hideAttempted) return _desktop.IsVisible() == true;
        // Own attempted change even if the package manager already removed the journal.
        bool? visible = _desktop.IsVisible();
        if (visible is null) return false;
        if (!visible.Value && !_desktop.SetVisible(true)) return false;
        return _desktop.IsVisible() == true && _lease.Restore();
    }

    private async Task<bool> RestoreCoreAsync()
    {
        if (_guardian is null) return !_hideAttempted || RestoreLocal() || Fail("Desktop restoration not verified.");
        var guardian = _guardian;
        // Whoever reaches a dead guardian first reports it, including a request that beats ObserveExitAsync.
        if (_hideAttempted && guardian.Exited.IsCompleted) _log(null, $"Desktop guardian exited while Desktop was hidden ({ExitDetail(guardian)}).");
        if (_hideAttempted && !guardian.Exited.IsCompleted)
        {
            try
            {
                if (await guardian.RestoreAsync().ConfigureAwait(false))
                    // Success ACK precedes the helper's journal cleanup. Do not
                    // race its file handle or terminate it during that cleanup.
                    await guardian.Exited.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
                else _log(null, "Desktop guardian refused to restore; restoring locally.");
            }
            catch (Exception ex) // Native fallback still required.
            {
                LastError = ex.Message;
                _log(ex, "Desktop guardian restore did not finish; restoring locally");
            }
        }
        if (!await guardian.CloseAfterLocalRestoreAsync(RestoreLocal).ConfigureAwait(false))
            return Fail("Desktop restoration not verified; keep recovery ownership.");
        _guardian = null;
        _hideAttempted = false;
        return true;
    }

    private async Task ObserveExitAsync(IDesktopGuardianSession guardian)
    {
        bool recovered = false;
        try
        {
            await guardian.Exited.ConfigureAwait(false);
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!ReferenceEquals(_guardian, guardian)) return;
                // Event-driven, no periodic health polling. Never hide again automatically.
                recovered = await RestoreCoreAsync().ConfigureAwait(false);
                LastError = recovered ? "Guardian exited; Desktop restored." : "Guardian exited; Desktop restoration failed.";
                _log(null, LastError);
            }
            finally { _gate.Release(); }
        }
        catch (Exception ex)
        {
            LastError = "Guardian exit recovery: " + ex.Message;
            _log(ex, "Guardian exit recovery");
        }
        GuardianExited?.Invoke(recovered);
    }

    private bool Fail(string message, Exception? error = null)
    {
        LastError = message;
        _log(error, message);
        return false;
    }

    // Hex, because that is how Windows crash codes such as 0xC0000005 are looked up.
    private static string ExitDetail(IDesktopGuardianSession guardian)
        => guardian.ExitCode is { } code ? $"exit code 0x{code:X8}" : "exit code unknown";
}
