using System.IO;
using Nepply.Services;

internal static class GuardedDesktopVisibilityTests
{
    private sealed class Desktop : IDesktopVisibility
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
    private sealed class Guardian(Desktop desktop) : IDesktopGuardianSession
    {
        internal readonly TaskCompletionSource Exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool CanArm = true, RestoreThrows, Armed, Closed;
        public Task Exited => Exit.Task;
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
            var flow = new GuardedDesktopVisibility(desktop, lease, () => { starts++; return Task.FromResult<IDesktopGuardianSession>(guardian); });
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
            () => Task.FromException<IDesktopGuardianSession>(new IOException("Injected startup failure")));
        check("Guarded startup failure never hides or claims Desktop",
            !await failedFlow.HideAsync() && failedDesktop.Hides == 0 && !failedLease.OwnsChange);
        check("Guarded startup failure still allows shutdown", await failedFlow.RestoreAsync(true));
    }

    private static async Task CheckPendingStartup(string root, Action<string, bool> check, bool stopping)
    {
        var desktop = new Desktop();
        var guardian = new Guardian(desktop);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource<IDesktopGuardianSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        int starts = 0;
        var lease = new DesktopVisibilityLease(desktop, Path.Combine(root, stopping ? "pending-stop.lease" : "pending-hides.lease"));
        var flow = new GuardedDesktopVisibility(desktop, lease, () => { starts++; started.TrySetResult(); return ready.Task; });
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
    }
}
