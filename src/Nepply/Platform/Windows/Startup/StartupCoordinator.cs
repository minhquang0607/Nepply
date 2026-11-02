namespace Nepply.Services;

internal sealed record StartupOutcome(bool? Enabled, UserMessage? Notice);

internal interface IStartupBackend
{
    Task<StartupOutcome> ApplyAsync(bool enabled, bool userInitiated, Func<bool> isCurrent);
}

// Owned by the UI thread. Serialize OS requests; discard superseded results.
internal sealed class StartupCoordinator(IStartupBackend backend) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _generation;
    private volatile bool _disposed;

    internal async Task<StartupOutcome?> ApplyAsync(bool enabled, bool userInitiated)
    {
        if (_disposed) return null;
        int generation = Interlocked.Increment(ref _generation);
        bool IsCurrent() => !_disposed && generation == Volatile.Read(ref _generation);
        await _gate.WaitAsync();
        try
        {
            if (!IsCurrent()) return null;
            var result = await backend.ApplyAsync(enabled, userInitiated, IsCurrent);
            return IsCurrent() ? result : null;
        }
        catch when (!IsCurrent()) { return null; }
        finally { _gate.Release(); }
    }

    // No semaphore WaitHandle is created. Do not dispose while continuations wait.
    public void Dispose() { _disposed = true; Interlocked.Increment(ref _generation); }
}

internal sealed class RegistryStartupBackend : IStartupBackend
{
    public Task<StartupOutcome> ApplyAsync(bool enabled, bool userInitiated, Func<bool> isCurrent)
    {
        if (!isCurrent()) return Task.FromResult(new StartupOutcome(null, null));
        var notice = AutoStartService.Apply(enabled);
        // Turning it off in Windows is a choice too: Settings shows it, only a request here warns.
        if (notice == null && enabled && userInitiated && AutoStartService.IsTurnedOffInWindows())
            return Task.FromResult(new StartupOutcome(true, UserMessage.FromKey(AutoStartService.TurnedOffNotice)));
        return Task.FromResult(new StartupOutcome(notice == null ? enabled : null, notice));
    }
}
