using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Nepply.Services;

internal static class ProductionGuardianCrashTests
{
    internal static int Owner(string root, string executable) => OwnerAsync(root, executable).GetAwaiter().GetResult();
    private static async Task<int> OwnerAsync(string root, string executable)
    {
        string journal = Path.Combine(root, "Nepply", "desktop-visibility.lease");
        var desktop = new ExplorerDesktopVisibility();
        var lease = new DesktopVisibilityLease(desktop, journal);
        DesktopGuardianConnection? connection = null;
        var flow = new GuardedDesktopVisibility(desktop, lease, async token =>
        {
            connection = await DesktopGuardianConnection.StartAsync(executable, token, journal);
            return connection;
        });
        try
        {
            if (!await flow.HideAsync() || connection is null) return 2;
            using var guardian = Process.GetProcessById(connection.ProcessId);
            File.WriteAllText(Path.Combine(root, "ready.json"), JsonSerializer.Serialize(new
            {
                Pid = guardian.Id,
                Ticks = guardian.StartTime.ToUniversalTime().Ticks
            }));
            await Task.Delay(TimeSpan.FromSeconds(60)); // Safety expiry if harness disappears.
            return 3;
        }
        finally { await flow.RestoreAsync(true); }
    }

    internal static int Run(string output, string executable) => RunAsync(output, executable).GetAwaiter().GetResult();
    private static async Task<int> RunAsync(string output, string executable)
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nepply-Guardian-QA", Guid.NewGuid().ToString("N"));
        string journal = Path.Combine(root, "Nepply", "desktop-visibility.lease");
        var desktop = new ExplorerDesktopVisibility();
        bool? initial = desktop.IsVisible();
        bool hidden = false, killed = false, recovered = false, rescue = false;
        Process? owner = null, guardian = null;
        string? error = null;
        try
        {
            if (initial != true || File.Exists(DesktopVisibilityLease.JournalPath)) throw new IOException("Desktop not available for isolated QA.");
            foreach (string name in new[] { "Nepply", "Nepply.DesktopGuardian" })
            {
                var processes = Process.GetProcessesByName(name);
                try { if (processes.Length > 0) throw new IOException("Close existing application before QA."); }
                finally { foreach (var process in processes) process.Dispose(); }
            }
            Directory.CreateDirectory(root);
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(root); start.ArgumentList.Add("--production-guardian-crash-owner"); start.ArgumentList.Add(executable);
            owner = Process.Start(start) ?? throw new IOException("QA owner did not start.");
            _ = owner.SafeHandle;
            string ready = Path.Combine(root, "ready.json");
            var deadline = Stopwatch.StartNew();
            while (!File.Exists(ready) && !owner.HasExited && deadline.Elapsed < TimeSpan.FromSeconds(30)) await Task.Delay(100);
            using var record = JsonDocument.Parse(File.ReadAllText(ready));
            guardian = Process.GetProcessById(record.RootElement.GetProperty("Pid").GetInt32());
            _ = guardian.SafeHandle;
            if (guardian.StartTime.ToUniversalTime().Ticks != record.RootElement.GetProperty("Ticks").GetInt64()
                || !string.Equals(guardian.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase))
                throw new IOException("QA guardian identity mismatch.");
            hidden = desktop.IsVisible() == false && File.Exists(journal);
            if (!hidden) throw new IOException("QA owner did not hide Desktop.");
            owner.Kill(); await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); killed = true;
            await guardian.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            recovered = guardian.ExitCode == 0 && desktop.IsVisible() == true && !File.Exists(journal);
        }
        catch (Exception ex) { error = ex.ToString(); }
        finally
        {
            try
            {
                if (owner is not null)
                {
                    if (!owner.HasExited) { owner.Kill(); await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
                    if (guardian is not null && !guardian.HasExited)
                    {
                        try { await guardian.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
                        catch (TimeoutException ex) { error = (error ?? "") + "\nGuardian cleanup timeout: " + ex.Message; }
                    }
                    if (desktop.IsVisible() != true || File.Exists(journal))
                    {
                        rescue = true;
                        desktop.SetVisible(true);
                        if (desktop.IsVisible() == true) new DesktopVisibilityLease(desktop, journal).Restore();
                    }
                }
            }
            catch (Exception ex) { error = (error ?? "") + "\nCleanup: " + ex; }
            finally { owner?.Dispose(); guardian?.Dispose(); }
        }
        bool passed = hidden && killed && recovered && !rescue && error is null;
        File.WriteAllText(Path.Combine(output, "production-guardian-crash.json"), JsonSerializer.Serialize(new
        {
            Passed = passed,
            Initial = initial,
            Hidden = hidden,
            OwnerKilled = killed,
            Recovered = recovered,
            RescueUsed = rescue,
            Final = desktop.IsVisible(),
            JournalRemains = File.Exists(journal),
            Journal = journal,
            Error = error,
            Scope = "Production guardian recovers real Explorer after abrupt QA owner exit; not installed/MSIX app acceptance."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return passed ? 0 : 1;
    }
}
