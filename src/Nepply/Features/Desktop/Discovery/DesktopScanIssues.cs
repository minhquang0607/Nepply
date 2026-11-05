using System.IO;

namespace Nepply.Services;

/// <summary>Entries one Desktop read left out. A bad entry is logged and skipped; only a lost source aborts the read.</summary>
internal sealed class DesktopScanIssues
{
    // A broken entry fails again on every later scan, so bound what one scan logs.
    internal const int MaximumLogged = 16;
    internal int Count { get; private set; }

    internal void Skip(string entry, Exception error)
    {
        Count++;
        if (Count <= MaximumLogged) AppLogger.Error(error, "Desktop entry skipped: " + entry);
        else if (Count == MaximumLogged + 1) AppLogger.Warn("Desktop scan skipped more entries than it logs.");
    }

    /// <summary>Several failures and no success look like a lost source, so the last snapshot must survive.</summary>
    internal static void RequireSomeRead(int attempted, int failed, string source)
    {
        if (failed > 1 && failed == attempted) throw new IOException("No entry could be read from " + source + ".");
    }
}
