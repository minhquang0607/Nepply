using System.Windows;
using Microsoft.Win32;

namespace Nepply.Services;

/// <summary>Owns Desktop update subscriptions as one lifetime, independent of panel presentation.</summary>
internal sealed class DesktopUpdateCoordinator : IDisposable
{
    private readonly DesktopWatcherService _watcher;
    private DesktopShellNotifications? _shell;
    private bool _disposed;
    public DesktopUpdateCoordinator(Window owner, DesktopIconService icons)
    {
        _watcher=new(icons);
        try { _shell=new(owner,_watcher.RequestChanges); }
        catch(Exception ex) { AppLogger.Error(ex,"Shell notifications unavailable; periodic Desktop reconciliation remains active"); }
        SystemEvents.PowerModeChanged+=PowerChanged;
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if(e.Mode==PowerModes.Resume) _watcher.RequestRefresh();
    }
    public void Dispose()
    {
        if(_disposed) return;
        _disposed=true;
        SystemEvents.PowerModeChanged-=PowerChanged;
        Helpers.CleanupSequence.Run(("Shell notifications",()=>_shell?.Dispose()),("Folder watcher",_watcher.Dispose));
    }
}
