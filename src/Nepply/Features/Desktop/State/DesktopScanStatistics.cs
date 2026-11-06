using System.Globalization;

namespace Nepply.Modules.Desktop;

/// <summary>
/// Counts Desktop scans so a long run can show how often the background repair re-reads
/// everything. It holds counts and durations only, never names or paths, and reports at
/// most once per <see cref="Window"/> so the log grows by a line an hour at most.
/// </summary>
internal sealed class DesktopScanStatistics(Func<DateTime>? utcNow = null)
{
    internal static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private readonly Func<DateTime> _utcNow = utcNow ?? (() => DateTime.UtcNow);
    private readonly object _gate = new();
    private DateTime? _windowStart;
    private int _full, _partial, _failed;
    private TimeSpan _readTime;

    /// <summary>Records one finished scan and returns the report line when a window has closed.</summary>
    internal string? Record(bool full, bool succeeded, TimeSpan readTime)
    {
        lock (_gate)
        {
            var now = _utcNow();
            _windowStart ??= now;
            if (full) _full++; else _partial++;
            if (!succeeded) _failed++;
            if (readTime > TimeSpan.Zero) _readTime += readTime;
            var elapsed = now - _windowStart.Value;
            if (elapsed < Window) return null;
            var line = string.Format(CultureInfo.InvariantCulture,
                "Desktop scans in the last {0:0} min: {1} full, {2} partial, {3} failed; reading took {4:0.0} s.",
                elapsed.TotalMinutes, _full, _partial, _failed, _readTime.TotalSeconds);
            _windowStart = now;
            _full = _partial = _failed = 0;
            _readTime = TimeSpan.Zero;
            return line;
        }
    }
}
