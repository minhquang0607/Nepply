using System.IO;
using Nepply.Services;

internal static class LogIsolationTests
{
    internal static void Run(Action<string, bool> check)
    {
        string userLogs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nepply", "Logs");
        string testLogs = Path.GetFullPath("test-logs", AppContext.BaseDirectory);

        check("Tests log beside the test binary, not into the user's logs",
            string.Equals(AppLogger.LogDirectory, testLogs, StringComparison.OrdinalIgnoreCase)
            && !AppLogger.LogDirectory.StartsWith(userLogs, StringComparison.OrdinalIgnoreCase));
        check("Every log file of the test process sits in the test folder",
            string.Equals(Path.GetDirectoryName(AppLogger.LogFile), testLogs, StringComparison.OrdinalIgnoreCase));
        check("Nepply without the switch keeps the user's log folder",
            AppLogger.ResolveLogDirectory(null, ["Nepply.exe"]) == userLogs
            && AppLogger.ResolveLogDirectory(" ", ["Nepply.exe", "--desktop-host"]) == userLogs);
        string previewLogs = Path.Combine(Path.GetTempPath(), "Nepply-preview", "Logs");
        check("Preview and smoke runs log apart from real use",
            AppLogger.PreviewArguments.All(arg => AppLogger.ResolveLogDirectory(null, ["Nepply.exe", arg]) == previewLogs));
        check("A preview flag must match exactly", AppLogger.ResolveLogDirectory(null, ["Nepply.exe", "--PREVIEW", "--preview-x"]) == userLogs);
        string absolute = Path.Combine(Path.GetTempPath(), "nepply-log-switch");
        check("An absolute switch value is used as given", AppLogger.ResolveLogDirectory(absolute, ["Nepply.exe"]) == absolute);
    }
}
