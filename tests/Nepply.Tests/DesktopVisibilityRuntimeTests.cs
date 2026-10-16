using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Nepply.Services;

internal static class DesktopVisibilityRuntimeTests
{
    internal static int Run(string output)
    {
        var desktop=new ExplorerDesktopVisibility();
        bool? initial=desktop.IsVisible(),afterHide=null,afterRestart=null,restored=null;
        var lease=new DesktopVisibilityLease(desktop,Path.Combine(output,"visibility.lease"));
        if(lease.OwnsChange) throw new InvalidOperationException("Existing test recovery journal must be handled first.");
        string? error=null;
        bool restartAcknowledged=false,restoreSucceeded=false;
        try
        {
            if(initial==true)
            {
                if(!lease.Hide()) throw new IOException("Could not hide Desktop icons.");
                afterHide=desktop.IsVisible();
                if(afterHide!=false) throw new IOException("Hidden state not confirmed.");
                File.WriteAllText(Path.Combine(output,"ready-for-explorer-restart"),Environment.ProcessId.ToString());
                var wait=Stopwatch.StartNew();
                while(!File.Exists(Path.Combine(output,"explorer-restarted")) && wait.Elapsed<TimeSpan.FromMinutes(3)) Thread.Sleep(100);
                restartAcknowledged=File.Exists(Path.Combine(output,"explorer-restarted"));
                if(!restartAcknowledged) throw new TimeoutException("Explorer restart was not acknowledged.");
                wait.Restart();
                do {afterRestart=desktop.IsVisible();if(afterRestart.HasValue) break;Thread.Sleep(100);}
                while(wait.Elapsed<TimeSpan.FromSeconds(15));
            }
        }
        catch(Exception ex) {error=ex.ToString();}
        finally
        {
            // Restore only the change owned by this journal. Never claim a pre-hidden Desktop.
            if(lease.OwnsChange)
            {
                var wait=Stopwatch.StartNew();
                do
                {
                    try {restoreSucceeded=lease.Restore();}
                    catch(Exception ex) {error=(error??"")+ex;}
                    if(restoreSucceeded) break;
                    Thread.Sleep(200);
                } while(wait.Elapsed<TimeSpan.FromSeconds(15));
            }
            restored=desktop.IsVisible();
        }
        bool passed=initial==true && afterHide==false && restartAcknowledged && afterRestart==false
            && restoreSucceeded && restored==true && !lease.OwnsChange && error==null;
        File.WriteAllText(Path.Combine(output,"desktop-visibility-runtime.json"),JsonSerializer.Serialize(new
        {
            Passed=passed,Initial=initial,AfterHide=afterHide,RestartAcknowledged=restartAcknowledged,
            AfterRestart=afterRestart,RestoreSucceeded=restoreSucceeded,Final=restored,JournalRemains=lease.OwnsChange,Error=error,
            Scope="Real Explorer Desktop visibility using production visibility lease with isolated recovery journal. No file changes. Pre-hidden/unavailable Desktop is skipped, not counted as pass."
        },new JsonSerializerOptions {WriteIndented=true}));
        Console.WriteLine($"Visibility initial={initial}; hidden={afterHide}; restart={afterRestart}; final={restored}; journal={lease.OwnsChange}");
        return passed ? 0 : 1;
    }
}
