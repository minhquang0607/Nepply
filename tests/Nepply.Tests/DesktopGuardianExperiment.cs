using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Text;
using Nepply.Services;

// Opt-in experiment only. No production activation, persistence, or public IPC contract.
internal static class DesktopGuardianExperiment
{
    private sealed class FileDesktop(string root) : IDesktopVisibility
    {
        public bool? IsVisible() => File.ReadAllText(Path.Combine(root, "desktop-state")) == "visible";
        public bool SetVisible(bool visible) { File.WriteAllText(Path.Combine(root, "desktop-state"), visible ? "visible" : "hidden"); return true; }
    }
    private static bool _fileFixture;
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPackageFullName(IntPtr process, ref uint length, StringBuilder? name);
    internal static int ExternalGuardian(string root, int ownerId, long ownerStarted)
    {
        File.WriteAllText(Path.Combine(root, "external-worker-start.json"), JsonSerializer.Serialize(new
        {
            ProcessId = Environment.ProcessId,
            Executable = Environment.ProcessPath,
            IsPackaged = ProcessPackageIdentity.IsPackaged,
            Package = ProcessPackageIdentity.IsPackaged ? Windows.ApplicationModel.Package.Current.Id.FullName : null
        }));
        if (ProcessPackageIdentity.IsPackaged) return 2;
        _fileFixture = true;
        return Guardian(root, ownerId, ownerStarted, external: true);
    }
    internal static int ExternalOwner(string root)
    {
        if (!ProcessPackageIdentity.IsPackaged || Windows.ApplicationModel.Package.Current.Id.Name != "Nepply.Internal.Preview") return 2;
        _fileFixture = true;
        File.WriteAllText(Path.Combine(root, "desktop-state"), "visible");
        // QA only: the harness supplies an explicit, already-published executable.
        // This tests actual packaged-parent activation, not product deployment.
        using var self = Process.GetCurrentProcess();
        Process? launched = null;
        string launchRequest = Path.Combine(root, "launch-executable");
        if (File.Exists(launchRequest))
        {
            string executable = File.ReadAllText(launchRequest);
            if (!Path.IsPathFullyQualified(executable) || Path.GetFileName(executable) != "Nepply.Tests.exe") return 8;
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(root);
            start.ArgumentList.Add("--guardian-external-worker");
            start.ArgumentList.Add(self.Id.ToString());
            start.ArgumentList.Add(self.StartTime.ToUniversalTime().Ticks.ToString());
            launched = Process.Start(start) ?? throw new IOException("Cannot launch external QA guardian.");
            File.WriteAllText(Path.Combine(root, "guardian-pid"), launched.Id.ToString());
        }
        var lease = Lease(root);
        try
        {
            var elapsed = Stopwatch.StartNew();
            while (!File.Exists(Path.Combine(root, "armed")) && elapsed.Elapsed < TimeSpan.FromSeconds(20)) Thread.Sleep(50);
            if (!File.Exists(Path.Combine(root, "armed"))) return 4;
            using var guardian = Process.GetProcessById(int.Parse(File.ReadAllText(Path.Combine(root, "armed"))));
            _ = guardian.Handle;
            if (guardian.SessionId != Process.GetCurrentProcess().SessionId || guardian.HasExited) return 5;
            if (!lease.Hide()) return 6;
            File.WriteAllText(Path.Combine(root, "hidden"), "ready");
            guardian.WaitForExit(30000);
            return Restore(root) ? 0 : 7;
        }
        finally { if (lease.OwnsChange) Restore(root); launched?.Dispose(); }
    }
    private static DesktopVisibilityLease Lease(string root)
    {
        string journalDirectory = root;
        string request = Path.Combine(root, "lease-directory");
        // Isolated QA fixture: Explorer state stays outside package, journal may be removed with it.
        if (_fileFixture && File.Exists(request)) journalDirectory = File.ReadAllText(request);
        return new(_fileFixture ? new FileDesktop(root) : new ExplorerDesktopVisibility(), Path.Combine(journalDirectory, "lease"));
    }
    internal static int PackagedOwner(string root)
    {
        if (!ProcessPackageIdentity.IsPackaged || Windows.ApplicationModel.Package.Current.Id.Name != "Nepply.Internal.Preview") return 2;
        _fileFixture = true;
        File.WriteAllText(Path.Combine(root, "desktop-state"), "visible");
        return Owner(root);
    }
    internal static int FileGuardian(string root, int ownerId, long ownerStarted)
    {
        _fileFixture = true;
        return Guardian(root, ownerId, ownerStarted);
    }
    private static Process Start(string root, params string[] args)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(root);
        foreach (string arg in args) start.ArgumentList.Add(arg);
        return Process.Start(start) ?? throw new IOException("Cannot launch QA process.");
    }
    private static bool Restore(string root)
    {
        var lease = Lease(root);
        var elapsed = Stopwatch.StartNew();
        do
        {
            try { if (lease.Restore()) return true; } catch (IOException) { }
            Thread.Sleep(100);
        } while (elapsed.Elapsed < TimeSpan.FromSeconds(10));
        return false;
    }
    internal static int Guardian(string root, int ownerId, long ownerStarted, bool external = false)
    {
        using var owner = Process.GetProcessById(ownerId);
        // Acquire a handle before handshake; verify that the PID wasn't reused.
        _ = owner.Handle;
        if (owner.StartTime.ToUniversalTime().Ticks != ownerStarted || owner.SessionId != Process.GetCurrentProcess().SessionId) return 2;
        if (external)
        {
            uint count = 0;
            if (GetPackageFullName(owner.Handle, ref count, null) != 122 || count > 1024) return 2;
            var name = new StringBuilder((int)count);
            if (GetPackageFullName(owner.Handle, ref count, name) != 0 || name.ToString() != "Nepply.Internal.Preview_0.0.3.0_x64__5vh1yvtztgq32") return 2;
        }
        else if (!string.Equals(owner.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) return 2;
        DesktopRecoverySession? session = null;
        Guid generation = Guid.NewGuid();
        if (external)
        {
            var desktop = new FileDesktop(root);
            session = new DesktopRecoverySession(generation, desktop.IsVisible, () => desktop.SetVisible(true));
            if (!session.Arm(generation)) return 4;
        }
        File.WriteAllText(Path.Combine(root, "armed"), Environment.ProcessId.ToString());
        owner.WaitForExit(); // No timer/polling while the owner lives.
        // QA external owner has no normal disarm path: it remains armed until terminated.
        // Recovery authority survives package data deletion, unlike a journal-only check.
        bool restored = (session is null || session.Recover(generation)) && Restore(root);
        File.WriteAllText(Path.Combine(root, "guardian-result.json"), JsonSerializer.Serialize(new { Restored = restored }));
        return restored ? 0 : 3;
    }
    internal static int Owner(string root)
    {
        var lease = Lease(root);
        using var self = Process.GetCurrentProcess();
        using var guardian = Start(root, _fileFixture ? "--guardian-file-worker" : "--guardian-worker", self.Id.ToString(), self.StartTime.ToUniversalTime().Ticks.ToString());
        File.WriteAllText(Path.Combine(root, "guardian-pid"), guardian.Id.ToString());
        try
        {
            var elapsed = Stopwatch.StartNew();
            while (!File.Exists(Path.Combine(root, "armed")) && !guardian.HasExited && elapsed.Elapsed < TimeSpan.FromSeconds(15)) Thread.Sleep(50);
            if (!File.Exists(Path.Combine(root, "armed")) || guardian.HasExited) return 4;
            if (!lease.Hide()) return 5;
            File.WriteAllText(Path.Combine(root, "hidden"), "ready");
            // Loss of guardian restores via the owner. Safety deadline bounds this QA experiment.
            bool guardianDied = guardian.WaitForExit(30000);
            bool restored = Restore(root);
            File.WriteAllText(Path.Combine(root, "owner-result.json"), JsonSerializer.Serialize(new { GuardianDied = guardianDied, Restored = restored }));
            return restored ? 0 : 6;
        }
        finally { if (lease.OwnsChange) Restore(root); }
    }
    internal static int Run(string output)
    {
        var results = new List<object>();
        bool allPassed = true;
        foreach (bool killGuardian in new[] { false, true })
        {
            string root = Path.Combine(output, "guardian-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Process? owner = null, guardian = null;
            bool ready = false, recovered = false, rescueUsed = false;
            string? error = null;
            double? idleCpuMs = null, workingSetMiB = null;
            try
            {
                if (new ExplorerDesktopVisibility().IsVisible() != true || File.Exists(DesktopVisibilityLease.JournalPath))
                    throw new InvalidOperationException("Desktop unavailable, already hidden, or owned by production; stop.");
                owner = Start(root, "--guardian-owner");
                var elapsed = Stopwatch.StartNew();
                while (!File.Exists(Path.Combine(root, "hidden")) && !owner.HasExited && elapsed.Elapsed < TimeSpan.FromSeconds(20)) Thread.Sleep(100);
                ready = File.Exists(Path.Combine(root, "hidden"));
                if (!ready) throw new IOException("Owner did not reach hidden state.");
                guardian = Process.GetProcessById(int.Parse(File.ReadAllText(Path.Combine(root, "guardian-pid"))));
                _ = guardian.Handle; // Retain the OS process object so ExitCode remains queryable after exit.
                if (!string.Equals(guardian.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) throw new IOException("Guardian identity mismatch.");
                guardian.Refresh();
                var cpu = guardian.TotalProcessorTime;
                Thread.Sleep(2000); // Harness measurement only; guardian blocks on process handle.
                guardian.Refresh();
                idleCpuMs = (guardian.TotalProcessorTime - cpu).TotalMilliseconds;
                workingSetMiB = guardian.WorkingSet64 / 1048576d;
                var victim = killGuardian ? guardian : owner;
                victim.Kill();
                if (!victim.WaitForExit(5000)) throw new IOException("Victim did not terminate.");
                var survivor = killGuardian ? owner : guardian;
                if (!survivor.WaitForExit(15000)) throw new IOException("Recovery actor did not finish.");
                string reportPath = Path.Combine(root, killGuardian ? "owner-result.json" : "guardian-result.json");
                using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
                recovered = survivor.ExitCode == 0 && report.RootElement.GetProperty("Restored").GetBoolean()
                    && new ExplorerDesktopVisibility().IsVisible() == true && !Lease(root).OwnsChange;
            }
            catch (Exception ex) { error = ex.ToString(); }
            finally
            {
                bool stopped = true;
                // On handshake failure the child may already have started its guardian.
                // Stop the owned parent first, then discover/wait that guardian before rescue.
                if (owner != null)
                {
                    try { if (!owner.HasExited) owner.Kill(); stopped = owner.WaitForExit(5000); }
                    catch (Exception ex) { stopped = false; error = (error ?? "") + ex; }
                }
                if (guardian == null && File.Exists(Path.Combine(root, "guardian-pid")))
                {
                    try
                    {
                        var candidate = Process.GetProcessById(int.Parse(File.ReadAllText(Path.Combine(root, "guardian-pid"))));
                        if (string.Equals(candidate.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) guardian = candidate;
                        else { candidate.Dispose(); stopped = false; }
                    }
                    catch (ArgumentException) { /* Process already exited. */ }
                    catch (Exception ex) { stopped = false; error = (error ?? "") + ex; }
                }
                if (guardian != null)
                {
                    try { guardian.WaitForExit(12000); }
                    catch (Exception ex) { stopped = false; error = (error ?? "") + ex; }
                }
                foreach (var process in new[] { owner, guardian })
                {
                    if (process == null) continue;
                    try { if (!process.HasExited) process.Kill(); stopped &= process.WaitForExit(5000); }
                    catch (Exception ex) { stopped = false; error = (error ?? "") + ex; }
                    process.Dispose();
                }
                if (stopped && Lease(root).OwnsChange) { rescueUsed = true; if (!Restore(root)) error = (error ?? "") + " Harness rescue failed; journal: " + root; }
            }
            bool passed = ready && recovered && !rescueUsed && error == null;
            allPassed &= passed;
            results.Add(new { Scenario = killGuardian ? "guardian-crash" : "owner-crash", Passed = passed, RecoveredByPeer = recovered, HarnessRescueUsed = rescueUsed, IdleCpuMsOver2Seconds = idleCpuMs, GuardianWorkingSetMiB = workingSetMiB, Root = root, Error = error });
            if (!passed) break;
        }
        File.WriteAllText(Path.Combine(output, "guardian-experiment.json"), JsonSerializer.Serialize(new { Passed = allPassed, Results = results, Scope = "Unpackaged QA processes on real Explorer. No MSIX removal, persistence, security certification or production integration." }, new JsonSerializerOptions { WriteIndented = true }));
        return allPassed ? 0 : 1;
    }
}
