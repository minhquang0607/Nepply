namespace Nepply.Services;

internal enum DesktopRecoveryResult { Restored=0, Failed=1, AppRunning=2, Unavailable=3 }

/// <summary>Recovery policy separated from mutex, Explorer and application startup for deterministic testing.</summary>
internal static class DesktopRecovery
{
    internal static DesktopRecoveryResult Run(Func<bool> acquire,Func<bool> restore,Action release)
    {
        bool owned=false;
        try
        {
            owned=acquire();
            if(!owned) return DesktopRecoveryResult.AppRunning;
            return restore() ? DesktopRecoveryResult.Restored : DesktopRecoveryResult.Unavailable;
        }
        catch(Exception ex) { AppLogger.Error(ex,"Desktop recovery"); return DesktopRecoveryResult.Failed; }
        finally { if(owned) release(); }
    }
}
