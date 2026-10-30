using Nepply.Services;

internal static class StartupCoordinatorTests
{
    private sealed class Backend : IStartupBackend
    {
        internal readonly TaskCompletionSource<StartupOutcome> First = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly List<(bool Enabled, bool User)> Calls = [];
        public Task<StartupOutcome> ApplyAsync(bool enabled, bool userInitiated, Func<bool> isCurrent)
        {
            Calls.Add((enabled, userInitiated));
            return Calls.Count == 1 ? First.Task : Task.FromResult(new StartupOutcome(enabled, null));
        }
    }
    internal static async Task RunAsync(Action<string, bool> check)
    {
        var backend = new Backend();
        using (var coordinator = new StartupCoordinator(backend))
        {
            var first = coordinator.ApplyAsync(true, true);
            var intermediate = coordinator.ApplyAsync(true, true);
            var latest = coordinator.ApplyAsync(false, true);
            check("Startup serializes pending OS calls", backend.Calls.Count == 1);
            backend.First.SetResult(new(true, null));
            check("Startup ignores obsolete result", await first == null);
            check("Startup skips superseded queued request", await intermediate == null);
            check("Startup newest request wins", (await latest)?.Enabled == false);
            check("Startup performs only initial and latest requests", backend.Calls.Count == 2 && backend.Calls[1] == (false, true));
        }
        backend = new Backend();
        using (var coordinator = new StartupCoordinator(backend))
        {
            var read = coordinator.ApplyAsync(true, false);
            check("Startup distinguishes initialization from user request", backend.Calls[0] == (true, false));
            backend.First.SetResult(new(false, null));
            check("Startup returns actual OS state", (await read)?.Enabled == false);
        }
        backend = new Backend();
        var disposed = new StartupCoordinator(backend);
        var pending = disposed.ApplyAsync(true, true);
        disposed.Dispose();
        backend.First.SetException(new InvalidOperationException("Fixture failure after shutdown"));
        check("Startup ignores obsolete exception after shutdown", await pending == null);
        check("Disposed startup coordinator rejects new work", await disposed.ApplyAsync(true, true) == null && backend.Calls.Count == 1);
        backend = new Backend();
        using (var coordinator = new StartupCoordinator(backend))
        {
            var failing = coordinator.ApplyAsync(true, true);
            backend.First.SetException(new InvalidOperationException("Current fixture failure"));
            bool surfaced = false;
            try { await failing; } catch (InvalidOperationException) { surfaced = true; }
            check("Current startup errors reach caller", surfaced);
            check("Startup gate released after failure", (await coordinator.ApplyAsync(false, true))?.Enabled == false);
        }
    }
}
