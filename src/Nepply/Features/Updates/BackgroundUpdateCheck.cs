using System.Net.NetworkInformation;
using Nepply.Services;

namespace Nepply.Modules.Updates;

/// <summary>
/// The daily check, on by default and off in Settings. It only reports that a version
/// exists: downloading and installing stay in the update window, behind explicit
/// consent. When Windows reports no network, nothing is sent and nothing counts as a
/// failure, so the check runs within an hour of the network coming back. A check that
/// fails while the network is up is logged and retried after <see cref="RetryDelay"/>;
/// the second failure in a row is reported once, until a check succeeds again.
/// </summary>
internal sealed class BackgroundUpdateCheck(
    Func<bool> enabled,
    Func<CancellationToken, Task<UpdateRelease?>> check,
    Action<UpdateRelease> notify,
    Func<DateTime>? utcNow = null,
    Func<bool>? networkAvailable = null,
    Action? notifyFailures = null) : IDisposable
{
    internal static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    internal static readonly TimeSpan RetryDelay = TimeSpan.FromHours(12);
    internal const int FailuresBeforeNotice = 2;
    private static readonly TimeSpan FirstDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Poll = TimeSpan.FromHours(1);
    private readonly Func<DateTime> _utcNow = utcNow ?? (() => DateTime.UtcNow);
    private readonly Func<bool> _networkAvailable = networkAvailable ?? NetworkInterface.GetIsNetworkAvailable;
    private readonly CancellationTokenSource _stop = new();
    private DateTime? _nextCheck;
    private int _failures;
    private bool _failureNoticeShown;
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
        if (!enabled() || now < _nextCheck) return;
        // Offline is not a failure: send nothing and look again at the next poll.
        if (!_networkAvailable()) return;
        UpdateRelease? release;
        try { release = await check(cancellation); }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellation.IsCancellationRequested)
        {
            _nextCheck = now + RetryDelay;
            _failures++;
            AppLogger.Info($"Background update check failed ({_failures} in a row): " + ex.GetType().Name);
            if (_failures >= FailuresBeforeNotice && !_failureNoticeShown && enabled())
            {
                _failureNoticeShown = true;
                notifyFailures?.Invoke();
            }
            return;
        }
        _nextCheck = now + Interval;
        _failures = 0;
        _failureNoticeShown = false;
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
