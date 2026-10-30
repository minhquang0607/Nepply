using System.Diagnostics;
using System.IO;
using Nepply.Services;

internal static class CrashRecoveryTests
{
    private sealed class FileDesktop(string state) : IDesktopVisibility
    {
        public bool? IsVisible() => File.Exists(state) ? File.ReadAllText(state) == "visible" : null;
        public bool SetVisible(bool visible) { File.WriteAllText(state, visible ? "visible" : "hidden"); return true; }
    }
    internal static int Child(string root)
    {
        var lease = new DesktopVisibilityLease(new FileDesktop(Path.Combine(root, "desktop-state")), Path.Combine(root, "lease"));
        if (!lease.Hide()) return 2;
        File.WriteAllText(Path.Combine(root, "ready"), "ready");
        Thread.Sleep(60000); return 3;
    }
    internal static int Run(string output)
    {
        string root = Path.Combine(output, "crash-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "desktop-state"), "visible");
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(root); start.ArgumentList.Add("--crash-lease-child");
        using var child = Process.Start(start) ?? throw new IOException("Cannot launch crash fixture.");
        using var exited = new ManualResetEventSlim();
        int exitNotifications = 0;
        using var exitSubscription = new Nepply.Services.Telemetry.ParentExitSubscription(child, () => { Interlocked.Increment(ref exitNotifications); exited.Set(); });
        bool ready = false;
        try
        {
            ready = SpinWait.SpinUntil(() => File.Exists(Path.Combine(root, "ready")) || child.HasExited, TimeSpan.FromSeconds(15)) && File.Exists(Path.Combine(root, "ready"));
        }
        finally { if (!child.HasExited) child.Kill(); child.WaitForExit(5000); }
        var results = new List<string>(); int failures = 0;
        void Check(string name, bool ok) { results.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) failures++; }
        Check("Child reached durable hide before termination", ready);
        Check("Process exit notification fires once without polling timer", exited.Wait(3000) && Volatile.Read(ref exitNotifications) == 1);
        var desktop = new FileDesktop(Path.Combine(root, "desktop-state"));
        var lease = new DesktopVisibilityLease(desktop, Path.Combine(root, "lease"));
        Check("Abrupt child termination preserves intent and hidden state", ready && lease.OwnsChange && desktop.IsVisible() == false);
        Check("Fresh recovery owner restores after process crash", ready && DesktopRecovery.Run(() => true, lease.Restore, () => { }) == DesktopRecoveryResult.Restored && desktop.IsVisible() == true && !lease.OwnsChange);
        Check("Recovery is idempotent", lease.Restore() && desktop.IsVisible() == true);
        results.Add($"RESULT: {results.Count - failures} passed; {failures} failed. Real child termination; file-backed Desktop fixture only. Not Explorer/uninstaller end-to-end.");
        File.WriteAllLines(Path.Combine(output, "verification.txt"), results); foreach (var line in results) Console.WriteLine(line);
        return failures == 0 ? 0 : 1;
    }
}
