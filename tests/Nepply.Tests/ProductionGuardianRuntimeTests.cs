using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Nepply.Services;

// Explicit opt-in. Uses the production pipe/coordinator/native code with a QA-only journal.
internal static class ProductionGuardianRuntimeTests
{
    internal static int Run(string output, string executable) => RunAsync(output, executable).GetAwaiter().GetResult();
    private static async Task<int> RunAsync(string output, string executable)
    {
        var desktop = new ExplorerDesktopVisibility();
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nepply-Guardian-QA", Guid.NewGuid().ToString("N"), "HoverBox");
        string journal = Path.Combine(root, "desktop-visibility.lease");
        var lease = new DesktopVisibilityLease(desktop, journal);
        DesktopGuardianConnection? connection = null;
        var flow = new GuardedDesktopVisibility(desktop, lease, async token =>
        {
            connection = await DesktopGuardianConnection.StartAsync(executable, token, journal);
            return connection;
        });
        bool? initial = desktop.IsVisible();
        bool started = false, hidden = false, restored = false, closed = false, rescueUsed = false;
        string? error = null;
        object? helperSample = null;
        try
        {
            if (initial != true || File.Exists(DesktopVisibilityLease.JournalPath))
                throw new InvalidOperationException("Desktop hidden/unavailable or production journal exists; no test performed.");
            foreach (string name in new[] { "Nepply", "HoverBox", "Nepply.DesktopGuardian" })
            {
                var processes = Process.GetProcessesByName(name);
                try { if (processes.Length != 0) throw new InvalidOperationException("Existing application/guardian must be closed before QA."); }
                finally { foreach (var process in processes) process.Dispose(); }
            }
            started = true;
            hidden = await flow.HideAsync() && desktop.IsVisible() == false && lease.OwnsChange;
            if (!hidden) throw new IOException(flow.LastError ?? "Production guarded hide failed.");
            using (var helper = Process.GetProcessById(connection!.ProcessId))
            {
                await Task.Delay(2000); // Exclude initialization from this short idle sample.
                helper.Refresh();
                TimeSpan cpu = helper.TotalProcessorTime;
                long privateBefore = helper.PrivateMemorySize64;
                int handlesBefore = helper.HandleCount;
                var elapsed = Stopwatch.StartNew();
                await Task.Delay(8000);
                helper.Refresh();
                helperSample = new
                {
                    Seconds = elapsed.Elapsed.TotalSeconds,
                    CpuSeconds = (helper.TotalProcessorTime - cpu).TotalSeconds,
                    PrivateBeforeBytes = privateBefore,
                    PrivateAfterBytes = helper.PrivateMemorySize64,
                    HandlesBefore = handlesBefore,
                    HandlesAfter = helper.HandleCount,
                    Threads = helper.Threads.Count,
                    Scope = "Short armed-idle sample, not long-run leak or wakeup certification."
                };
            }
            restored = await flow.RestoreAsync(true) && desktop.IsVisible() == true && !lease.OwnsChange;
            if (!restored) error = flow.LastError ?? "Restoration verification failed.";
            await flow.RecoveryObservation.WaitAsync(TimeSpan.FromSeconds(15));
            closed = connection is not null && connection.Exited.IsCompletedSuccessfully;
        }
        catch (Exception ex) { error = ex.ToString(); }
        finally
        {
            if (started && (desktop.IsVisible() != true || lease.OwnsChange || connection is { Exited.IsCompleted: false }))
            {
                rescueUsed = true;
                try
                {
                    await flow.RestoreAsync(true);
                    // Hide has completed and no further hides are queued. Rescue is not a pass.
                    if (desktop.IsVisible() != true) desktop.SetVisible(true);
                    if (desktop.IsVisible() == true) lease.Restore();
                    if (connection is not null) await connection.CloseAfterLocalRestoreAsync(() => desktop.IsVisible() == true);
                }
                catch (Exception ex) { error = (error ?? "") + "\nRescue: " + ex; }
            }
        }
        bool passed = hidden && restored && closed && !rescueUsed && error is null && desktop.IsVisible() == initial;
        File.WriteAllText(Path.Combine(output, "production-guardian-runtime.json"), JsonSerializer.Serialize(new
        {
            Passed = passed,
            Initial = initial,
            Hidden = hidden,
            Restored = restored,
            Closed = closed,
            RescueUsed = rescueUsed,
            Final = desktop.IsVisible(),
            JournalRemains = File.Exists(journal),
            Journal = journal,
            Error = error,
            HelperSample = helperSample,
            Scope = "Production guardian normal hide/restore on real Explorer, isolated QA journal. Not crash/MSIX uninstall acceptance."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return passed ? 0 : 1;
    }
}
