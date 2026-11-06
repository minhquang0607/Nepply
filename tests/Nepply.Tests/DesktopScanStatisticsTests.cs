using Nepply.Modules.Desktop;

internal static class DesktopScanStatisticsTests
{
    internal static void Run(Action<string, bool> check)
    {
        var now = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
        var statistics = new DesktopScanStatistics(() => now);

        check("The first scan opens the window without a report", statistics.Record(true, true, TimeSpan.FromMilliseconds(800)) == null);
        now += TimeSpan.FromMinutes(5);
        check("A partial scan inside the window stays silent", statistics.Record(false, true, TimeSpan.FromMilliseconds(200)) == null);
        now += TimeSpan.FromMinutes(5);
        check("A failed scan inside the window stays silent", statistics.Record(true, false, TimeSpan.FromMilliseconds(1000)) == null);

        now += TimeSpan.FromMinutes(50);
        var line = statistics.Record(true, true, TimeSpan.FromMilliseconds(500));
        check("The scan that closes the hour reports every scan in it",
            line == "Desktop scans in the last 60 min: 3 full, 1 partial, 1 failed; reading took 2.5 s.");
        check("The report holds counts only, no paths", line != null && !line.Contains('\\') && !line.Contains('/'));

        now += TimeSpan.FromMinutes(1);
        check("The next window starts empty", statistics.Record(false, true, TimeSpan.Zero) == null);
        now += TimeSpan.FromHours(3);
        check("A quiet stretch reports its real length",
            statistics.Record(false, true, TimeSpan.Zero) == "Desktop scans in the last 181 min: 0 full, 2 partial, 0 failed; reading took 0.0 s.");
    }
}
