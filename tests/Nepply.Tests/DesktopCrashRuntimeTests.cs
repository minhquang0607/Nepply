using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Nepply.Services;

// Explicit opt-in only. The parent is a QA rescue process, not a shipping watchdog.
internal static class DesktopCrashRuntimeTests
{
    internal static int Child(string root)
    {
        var lease = new DesktopVisibilityLease(new ExplorerDesktopVisibility(), Path.Combine(root, "lease"));
        if (!lease.Hide()) return 2;
        File.WriteAllText(Path.Combine(root, "ready"), Environment.ProcessId.ToString());
        Thread.Sleep(TimeSpan.FromSeconds(45));
        lease.Restore();
        return 3;
    }

    internal static int Run(string output)
    {
        var desktop = new ExplorerDesktopVisibility();
        bool? initial = desktop.IsVisible();
        string root = Path.Combine(output, "desktop-crash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var lease = new DesktopVisibilityLease(desktop, Path.Combine(root, "lease"));
        bool ready = false, terminated = false, restored = false;
        bool? afterCrash = null;
        string? error = null;
        Process? child = null;
        try
        {
            if (initial != true) throw new InvalidOperationException("Desktop already hidden/unavailable; no changes made, not a pass.");
            if (File.Exists(DesktopVisibilityLease.JournalPath)) throw new InvalidOperationException("Production owns a Desktop lease; stop.");
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(root);
            start.ArgumentList.Add("--desktop-crash-child");
            child = Process.Start(start) ?? throw new IOException("Cannot start owned QA child.");
            var deadline = Stopwatch.StartNew();
            while (!File.Exists(Path.Combine(root, "ready")) && !child.HasExited && deadline.Elapsed < TimeSpan.FromSeconds(15)) Thread.Sleep(100);
            ready = File.Exists(Path.Combine(root, "ready"));
            if (!ready) throw new IOException("QA child did not reach hidden state.");
            child.Kill();
            terminated = child.WaitForExit(5000);
            if (!terminated) throw new IOException("Child has not terminated; rescue must not race its hide operation.");
            afterCrash = desktop.IsVisible();
        }
        catch (Exception ex) { error = ex.ToString(); }
        finally
        {
            bool safeToRestore = child == null;
            if (child != null)
            {
                try { if (!child.HasExited) child.Kill(); safeToRestore = child.WaitForExit(10000); }
                catch (Exception ex) { error = (error ?? "") + ex; }
                child.Dispose();
            }
            if (safeToRestore && lease.OwnsChange)
            {
                var deadline = Stopwatch.StartNew();
                do
                {
                    try { restored = lease.Restore(); }
                    catch (Exception ex) { error = (error ?? "") + ex; }
                    if (restored) break;
                    Thread.Sleep(200);
                } while (deadline.Elapsed < TimeSpan.FromSeconds(15));
            }
        }
        bool? final = desktop.IsVisible();
        bool passed = ready && terminated && afterCrash == false && restored && final == initial && !lease.OwnsChange && error == null;
        File.WriteAllText(Path.Combine(output, "desktop-crash-result.json"), JsonSerializer.Serialize(new
        {
            Passed = passed, Initial = initial, Ready = ready, ChildTerminated = terminated,
            AfterCrash = afterCrash, Restored = restored, Final = final, JournalRemains = lease.OwnsChange,
            Journal = Path.Combine(root, "lease"), Error = error,
            Scope = "Real Explorer hide and abrupt owned-child termination; production lease restores via QA parent. NOT automatic production/MSIX uninstall recovery."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return passed ? 0 : 1;
    }
}
