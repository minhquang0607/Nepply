using Nepply.Services;

namespace Nepply.Modules.Updates;

/// <summary>
/// The optional daily check, off by default. It only reports that a version
/// exists: downloading and installing stay in the update window, behind explicit
/// consent. A failed check is logged and waits for the next interval, so an
/// offline machine neither retries in a loop nor bothers the user.
/// </summary>
internal sealed class BackgroundUpdateCheck(
    Func<bool> enabled,
    Func<CancellationToken, Task<UpdateRelease?>> check,
    Action<UpdateRelease> notify,
    Func<DateTime>? utcNow = null) : IDisposable
{
    internal static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan FirstDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Poll = TimeSpan.FromHours(1);
    private readonly Func<DateTime> _utcNow = utcNow ?? (() => DateTime.UtcNow);
    private readonly CancellationTokenSource _stop = new();
    private DateTime? _lastCheck;
    private string? _lastNotified;

    /// <summary>Runs on the caller's context, so <c>enabled</c> and <c>notify</c> stay on the UI thread.</summary>
    internal async Task RunAsync()
    {
        var stop = _stop.Token;
        try
        {
            await Task.Delay(FirstDelay, stop);
            while (true)
            {
                await TickAsync(stop);
                await Task.Delay(Poll, stop);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    internal async Task TickAsync(CancellationToken cancellation)
    {
        var now = _utcNow();
        if (!enabled() || now - _lastCheck < Interval) return;
        _lastCheck = now;
        UpdateRelease? release;
        try { release = await check(cancellation); }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellation.IsCancellationRequested)
        {
            AppLogger.Info("Background update check failed: " + ex.GetType().Name);
            return;
        }
        // The setting may have been turned off while the request ran.
        if (release == null || release.Tag == _lastNotified || !enabled()) return;
        _lastNotified = release.Tag;
        notify(release);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }
}
