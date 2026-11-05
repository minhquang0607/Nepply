using System.IO;
using Nepply.Services;

internal static class GuardedDesktopVisibilityTests
{
    internal sealed class Desktop : IDesktopVisibility
    {
        internal bool? Visible = true;
        internal bool ThrowAfterHide, FailShow;
        internal int Hides, Shows;
        public bool? IsVisible() => Visible;
        public bool SetVisible(bool visible)
        {
            if (visible) { Shows++; if (FailShow) return false; } else Hides++;
            Visible = visible;
            if (!visible && ThrowAfterHide) throw new IOException("Injected hide failure");
            return true;
        }
    }
    internal sealed class Guardian(Desktop desktop) : IDesktopGuardianSession
    {
        internal readonly TaskCompletionSource Exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool CanArm = true, RestoreThrows, CloseThrows, Armed, Closed;
        public Task Exited => Exit.Task;
        public int? ExitCode { get; set; }
        public Task<bool> ArmAsync(CancellationToken cancellation = default) { Armed = CanArm; return Task.FromResult(CanArm); }
        public Task<bool> RestoreAsync(CancellationToken cancellation = default)
        {
            if (RestoreThrows) throw new IOException("Injected lost pipe");
            bool restored = !Armed || desktop.SetVisible(true);
            if (restored) Exit.TrySetResult();
            return Task.FromResult(restored);
        }
        public Task<bool> CloseAfterLocalRestoreAsync(Func<bool> restore)
        {
            if (CloseThrows) throw new IOException("Injected close failure");
            if (!restore()) return Task.FromResult(false);
            Closed = true; Armed = false; Exit.TrySetResult(); return Task.FromResult(true);
        }
        public ValueTask DisposeAsync() { Closed = true; Exit.TrySetResult(); return ValueTask.CompletedTask; }
    }
    internal static void Run(string output, Action<string, bool> check) => RunAsync(output, check).GetAwaiter().GetResult();
    private static async Task RunAsync(string output, Action<string, bool> check)
    {
        string root = Path.Combine(output, "guarded-visibility-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        foreach (string scenario in new[] { "prehidden", "no-arm", "normal", "hide-throws", "helper-dies", "pipe-lost", "journal-lost", "restore-fails" })
        {
            var desktop = new Desktop(); var guardian = new Guardian(desktop); int starts = 0;
            string journal = Path.Combine(root, scenario + ".lease");
            var lease = new DesktopVisibilityLease(desktop, journal);
            var flow = new GuardedDesktopVisibility(desktop, lease, () => { starts++; return Task.FromResult<IDesktopGuardianSession>(guardian); }, Quiet);
            if (scenario == "prehidden") desktop.Visible = false;
            if (scenario == "no-arm") guardian.CanArm = false;
            if (scenario == "hide-throws") desktop.ThrowAfterHide = true;
            bool hidden = await flow.HideAsync();
            if (scenario == "prehidden")
            {
                check("Guarded flow preserves prehidden Desktop without helper", hidden && starts == 0 && desktop.Hides == 0 && !lease.OwnsChange);
                check("Guarded shutdown preserves prehidden Desktop", await flow.RestoreAsync(true) && desktop.Visible == false);
                continue;
            }
            if (scenario == "no-arm" || scenario == "hide-throws")
            {
                check("Guarded " + scenario + " leaves visible Desktop and no lease", !hidden && desktop.Visible == true && !lease.OwnsChange && guardian.Closed);
                await flow.RecoveryObservation;
                continue;
            }
            check("Guarded " + scenario + " arms before owning hidden Desktop", hidden && guardian.Armed && desktop.Visible == false && lease.OwnsChange);
            if (scenario == "helper-dies")
            {
                guardian.Exit.TrySetResult(); await flow.RecoveryObservation;
                check("Guarded flow restores on helper exit without polling", desktop.Visible == true && !lease.OwnsChange && guardian.Closed);
                continue;
            }
            if (scenario == "journal-lost") File.Delete(journal); // Only this test's generated fixture.
            if (scenario == "pipe-lost") guardian.RestoreThrows = true;
            if (scenario == "restore-fails")
            {
                desktop.FailShow = true;
                check("Guarded failed restore retains journal and guardian", !await flow.RestoreAsync() && lease.OwnsChange && !guardian.Closed && desktop.Visible == false);
                desktop.FailShow = false;
            }
            check("Guarded " + scenario + " restores and closes", await flow.RestoreAsync(true) && desktop.Visible == true && !lease.OwnsChange && guardian.Closed);
            await flow.RecoveryObservation;
            check("Guarded stopped flow cannot hide again: " + scenario, !await flow.HideAsync() && desktop.Visible == true);
        }
        await CheckPendingStartup(root, check, stopping: false);
        await CheckPendingStartup(root, check, stopping: true);

        var failedDesktop = new Desktop();
        var failedLease = new DesktopVisibilityLease(failedDesktop, Path.Combine(root, "startup-failed.lease"));
        var failedFlow = new GuardedDesktopVisibility(failedDesktop, failedLease,
            () => Task.FromException<IDesktopGuardianSession>(new IOException("Injected startup failure")), Quiet);
        check("Guarded startup failure never hides or claims Desktop",
            !await failedFlow.HideAsync() && failedDesktop.Hides == 0 && !failedLease.OwnsChange);
        check("Guarded startup failure still allows shutdown", await failedFlow.RestoreAsync(true));
        await CheckReporting(root, check);
    }

    private static readonly Action<Exception?, string> Quiet = static (_, _) => { };

    private sealed class Reported
    {
        internal readonly Desktop Desktop = new();
        internal readonly Guardian Guardian;
        internal readonly DesktopVisibilityLease Lease;
        internal readonly GuardedDesktopVisibility Flow;
        internal readonly List<(Exception? Error, string Message)> Log = [];
        internal readonly List<bool> Exits = [];
        internal string Text { get { lock (Log) return string.Join("\n", Log.Select(entry => entry.Message + " " + entry.Error?.Message)); } }
        internal Reported(string root, string name, Func<Task<IDesktopGuardianSession>>? start = null)
        {
            Guardian = new(Desktop);
            Lease = new(Desktop, Path.Combine(root, "reported-" + name + ".lease"));
            Flow = new(Desktop, Lease, start ?? (() => Task.FromResult<IDesktopGuardianSession>(Guardian)),
                (error, message) => { lock (Log) Log.Add((error, message)); });
            Flow.GuardianExited += Exits.Add;
        }
        internal Task Kill(int code)
        {
            Guardian.ExitCode = code;
            Guardian.Exit.TrySetResult();
            return Flow.RecoveryObservation.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    // F-06: every guardian failure reaches the log, and only a guardian death reaches the panel.
    private static async Task CheckReporting(string root, Action<string, bool> check)
    {
        var quiet = new Reported(root, "normal");
        await quiet.Flow.HideAsync();
        await quiet.Flow.RestoreAsync();
        await quiet.Flow.RecoveryObservation;
        check("Guarded normal hide and restore log nothing and raise nothing", quiet.Log.Count == 0 && quiet.Exits.Count == 0);

        var stopped = new Reported(root, "stopped");
        await stopped.Flow.HideAsync();
        await stopped.Flow.RestoreAsync(true);
        await stopped.Flow.RecoveryObservation;
        check("Guarded app shutdown is not reported as a guardian death", stopped.Log.Count == 0 && stopped.Exits.Count == 0);

        var killed = new Reported(root, "killed");
        await killed.Flow.HideAsync();
        await killed.Kill(-1);
        check("Guardian death logs its exit code", killed.Text.Contains("exit code 0xFFFFFFFF"));
        check("Guardian death logs the restored outcome", killed.Text.Contains("Guardian exited; Desktop restored."));
        check("Guardian death raises one restored event", killed.Exits.SequenceEqual([true]) && killed.Desktop.Visible == true && !killed.Lease.OwnsChange);
        await killed.Flow.RestoreAsync(true);
        check("Guardian death is not raised again on shutdown", killed.Exits.Count == 1);

        var unknown = new Reported(root, "unknown");
        await unknown.Flow.HideAsync();
        unknown.Guardian.Exit.TrySetResult();
        await unknown.Flow.RecoveryObservation.WaitAsync(TimeSpan.FromSeconds(5));
        check("Guardian death without an exit code still logs", unknown.Text.Contains("exit code unknown"));

        var stuck = new Reported(root, "stuck");
        await stuck.Flow.HideAsync();
        stuck.Desktop.FailShow = true;
        await stuck.Kill(3);
        check("Guardian death with failed restore raises a failed event", stuck.Exits.SequenceEqual([false]) && stuck.Lease.OwnsChange && !stuck.Guardian.Closed);
        check("Guardian death with failed restore logs the failure", stuck.Text.Contains("exit code 0x00000003") && stuck.Text.Contains("Guardian exited; Desktop restoration failed."));
        stuck.Desktop.FailShow = false;
        check("Guardian death with failed restore can still recover on shutdown", await stuck.Flow.RestoreAsync(true) && stuck.Desktop.Visible == true && !stuck.Lease.OwnsChange);

        var broken = new Reported(root, "broken");
        await broken.Flow.HideAsync();
        broken.Guardian.CloseThrows = true;
        await broken.Kill(-1);
        check("Guardian exit recovery exception keeps its detail in the log",
            broken.Exits.SequenceEqual([false]) && broken.Log.Any(entry => entry.Error?.Message == "Injected close failure"));
        broken.Guardian.CloseThrows = false;
        await broken.Flow.RestoreAsync(true);

        var raced = new Reported(root, "raced");
        await raced.Flow.HideAsync();
        raced.Guardian.ExitCode = -1;
        raced.Guardian.Exit.TrySetResult();
        bool racedRestore = await raced.Flow.RestoreAsync(); // Either side may win the gate; neither may lose the report.
        await raced.Flow.RecoveryObservation.WaitAsync(TimeSpan.FromSeconds(5));
        check("Guardian death is logged once even when a request beats its observer",
            racedRestore && raced.Log.Count(entry => entry.Message.Contains("exit code 0xFFFFFFFF")) == 1);

        var noArm = new Reported(root, "no-arm");
        noArm.Guardian.CanArm = false;
        check("Guardian refusing to arm is logged", !await noArm.Flow.HideAsync() && noArm.Text.Contains("Guardian did not arm."));

        var dead = new Reported(root, "dead-before-arm");
        dead.Guardian.ExitCode = 3;
        dead.Guardian.Exit.TrySetResult();
        check("Guardian dying before arm logs its exit code", !await dead.Flow.HideAsync() && dead.Text.Contains("Guardian did not arm (exit code 0x00000003)."));
        await dead.Flow.RecoveryObservation.WaitAsync(TimeSpan.FromSeconds(5));
        check("Guardian dying before arm is a hide failure, not a guardian death", dead.Exits.Count == 0);

        var throwing = new Reported(root, "hide-throws");
        throwing.Desktop.ThrowAfterHide = true;
        check("Native hide exception reaches the log", !await throwing.Flow.HideAsync() && throwing.Log.Any(entry => entry.Error?.Message == "Injected hide failure"));

        var missing = new Reported(root, "startup", () => Task.FromException<IDesktopGuardianSession>(new IOException("Injected startup failure")));
        check("Guardian startup exception reaches the log", !await missing.Flow.HideAsync() && missing.Log.Any(entry => entry.Error?.Message == "Injected startup failure"));

        var unavailable = new Reported(root, "unavailable");
        unavailable.Desktop.Visible = null;
        check("Unavailable Desktop reaches the log", !await unavailable.Flow.HideAsync() && unavailable.Text.Contains("Desktop unavailable."));

        var refused = new Reported(root, "restore-fails");
        await refused.Flow.HideAsync();
        refused.Desktop.FailShow = true;
        check("Failed restore reaches the log", !await refused.Flow.RestoreAsync() && refused.Text.Contains("refused to restore") && refused.Text.Contains("not verified"));
        check("Failed shutdown restore reaches the log", !await refused.Flow.RestoreAsync(true) && refused.Log.Count(entry => entry.Message.Contains("not verified")) == 2);
        refused.Desktop.FailShow = false;
        await refused.Flow.RestoreAsync(true);

        var lost = new Reported(root, "pipe-lost");
        await lost.Flow.HideAsync();
        lost.Guardian.RestoreThrows = true;
        check("Lost guardian pipe is logged although the local restore succeeds", await lost.Flow.RestoreAsync(true) && lost.Log.Any(entry => entry.Error?.Message == "Injected lost pipe"));
    }

    private static async Task CheckPendingStartup(string root, Action<string, bool> check, bool stopping)
    {
        var desktop = new Desktop();
        var guardian = new Guardian(desktop);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource<IDesktopGuardianSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        int starts = 0;
        var lease = new DesktopVisibilityLease(desktop, Path.Combine(root, stopping ? "pending-stop.lease" : "pending-hides.lease"));
        var logged = new List<string>();
        var flow = new GuardedDesktopVisibility(desktop, lease, () => { starts++; started.TrySetResult(); return ready.Task; }, (_, message) => logged.Add(message));
        Task<bool> first = flow.HideAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<bool>[] queued = stopping ? [flow.RestoreAsync(true)] : Enumerable.Range(0, 12).Select(_ => flow.HideAsync()).ToArray();
        try
        {
            check("Guarded pending startup performs no native hide: " + stopping, desktop.Hides == 0 && !lease.OwnsChange);
            check("Guarded operations queue behind startup: " + stopping, queued.All(task => !task.IsCompleted));
        }
        finally { ready.TrySetResult(guardian); }
        bool initial = await first.WaitAsync(TimeSpan.FromSeconds(5));
        bool[] results = await Task.WhenAll(queued).WaitAsync(TimeSpan.FromSeconds(5));
        check("Guarded concurrent requests honor stop before native hide: " + stopping,
            initial == !stopping && results.All(ok => ok) && starts == 1 && desktop.Hides == (stopping ? 0 : 1));
        if (stopping)
        {
            check("Guarded shutdown during startup restores and closes",
                desktop.Visible == true && !lease.OwnsChange && guardian.Closed);
            check("Guarded shutdown fences subsequent hide", !await flow.HideAsync());
        }
        else
        {
            check("Guarded repeated hide retains a single armed owner", guardian.Armed && !guardian.Closed && lease.OwnsChange);
            check("Guarded concurrent-hide cleanup succeeds", await flow.RestoreAsync(true));
        }
        await flow.RecoveryObservation.WaitAsync(TimeSpan.FromSeconds(5));
        check("Guarded shutdown during startup logs no failure: " + stopping, logged.Count == 0);
    }
}
