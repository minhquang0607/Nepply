namespace Nepply.Services;

/// <summary>Serializes hide/restore with guardian readiness. No UI or deployment ownership.</summary>
internal sealed class GuardedDesktopVisibility
{
    private readonly IDesktopVisibility _desktop;
    private readonly DesktopVisibilityLease _lease;
    private readonly Func<CancellationToken,Task<IDesktopGuardianSession>> _start;
    private readonly CancellationTokenSource _shutdown=new();
    private readonly SemaphoreSlim _gate=new(1,1);
    private IDesktopGuardianSession? _guardian;
    private bool _hideAttempted, _stopping;
    internal Task RecoveryObservation {get;private set;}=Task.CompletedTask;
    internal string? LastError {get;private set;}

    internal GuardedDesktopVisibility(IDesktopVisibility desktop,DesktopVisibilityLease lease,Func<Task<IDesktopGuardianSession>> start)
        :this(desktop,lease,_=>start()) {}

    internal GuardedDesktopVisibility(IDesktopVisibility desktop,DesktopVisibilityLease lease,Func<CancellationToken,Task<IDesktopGuardianSession>> start)
    {_desktop=desktop;_lease=lease;_start=start;}

    internal async Task<bool> HideAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if(_stopping || _shutdown.IsCancellationRequested) return false;
            LastError=null;
            if(_guardian is not null)
            {
                if(_hideAttempted && !_guardian.Exited.IsCompleted && _desktop.IsVisible()==false) return true;
                if(!await RestoreCoreAsync().ConfigureAwait(false)) return false;
            }
            bool? visible=_desktop.IsVisible();
            if(visible==false) return true; // User-owned hidden Desktop, no helper and no lease.
            if(visible!=true) return Fail("Desktop unavailable.");
            _guardian=await _start(_shutdown.Token).ConfigureAwait(false);
            var guardian=_guardian;
            RecoveryObservation=ObserveExitAsync(guardian);
            _shutdown.Token.ThrowIfCancellationRequested();
            if(guardian.Exited.IsCompleted || !await guardian.ArmAsync(_shutdown.Token).ConfigureAwait(false))
            {
                await RestoreCoreAsync().ConfigureAwait(false);
                return Fail("Guardian did not arm.");
            }
            _hideAttempted=true; // Native hide may change Explorer and then throw.
            if(!_lease.Hide() || guardian.Exited.IsCompleted)
            {
                await RestoreCoreAsync().ConfigureAwait(false);
                return Fail("Desktop hide did not complete with a live guardian.");
            }
            return true;
        }
        catch(Exception ex)
        {
            LastError=ex.Message;
            try{await RestoreCoreAsync().ConfigureAwait(false);}
            catch(Exception recovery){LastError+="; "+recovery.Message;}
            return false;
        }
        finally{_gate.Release();}
    }

    internal async Task<bool> RestoreAsync(bool stopping=false)
    {
        if(stopping) _shutdown.Cancel(); // Interrupt startup before waiting for its operation gate.
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _stopping|=stopping;
            return await RestoreCoreAsync().ConfigureAwait(false);
        }
        catch(Exception ex){return Fail(ex.Message);}
        finally{_gate.Release();}
    }

    private bool RestoreLocal()
    {
        if(!_hideAttempted) return _desktop.IsVisible()==true;
        // Own attempted change even if the package manager already removed the journal.
        bool? visible=_desktop.IsVisible();
        if(visible is null) return false;
        if(!visible.Value && !_desktop.SetVisible(true)) return false;
        return _desktop.IsVisible()==true && _lease.Restore();
    }

    private async Task<bool> RestoreCoreAsync()
    {
        if(_guardian is null) return !_hideAttempted || RestoreLocal();
        var guardian=_guardian;
        if(_hideAttempted && !guardian.Exited.IsCompleted)
        {
            try
            {
                if(await guardian.RestoreAsync().ConfigureAwait(false))
                    // Success ACK precedes the helper's journal cleanup. Do not
                    // race its file handle or terminate it during that cleanup.
                    await guardian.Exited.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            }
            catch(Exception ex){LastError=ex.Message;} // Native fallback still required.
        }
        if(!await guardian.CloseAfterLocalRestoreAsync(RestoreLocal).ConfigureAwait(false))
            return Fail("Desktop restoration not verified; keep recovery ownership.");
        _guardian=null;
        _hideAttempted=false;
        return true;
    }

    private async Task ObserveExitAsync(IDesktopGuardianSession guardian)
    {
        try
        {
            await guardian.Exited.ConfigureAwait(false);
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if(!ReferenceEquals(_guardian,guardian)) return;
                // Event-driven, no periodic health polling. Never hide again automatically.
                bool recovered=await RestoreCoreAsync().ConfigureAwait(false);
                LastError=recovered ? "Guardian exited; Desktop restored." : "Guardian exited; Desktop restoration failed.";
            }
            finally{_gate.Release();}
        }
        catch(Exception ex){LastError="Guardian exit recovery: "+ex.Message;}
    }

    private bool Fail(string message){LastError=message;return false;}
}
