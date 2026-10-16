using System.IO;
using System.Text.Json;
using Nepply.Services;

internal static class DesktopDiscoveryTests
{
    internal static int Run(string output, string? executable)
    {
        using var scanner = new DesktopIconService(executable);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        scanner.ScanDesktopIconsAsync().WaitAsync(TimeSpan.FromSeconds(35)).GetAwaiter().GetResult();
        var icons = scanner.GetIcons();
        var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) };
        var expected = roots.SelectMany(Directory.EnumerateFileSystemEntries)
            .Where(p => !Path.GetFileName(p).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)
                && (File.GetAttributes(p) & FileAttributes.Hidden) == 0).ToArray();
        var represented = icons.SelectMany(i => i.SourcePaths.Length > 0 ? i.SourcePaths : [i.Path])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = expected.Where(p => !represented.Contains(p)).ToArray();
        bool passed = scanner.HasScanned && scanner.ScanState.Error == null && missing.Length == 0;
        // Read-only real-helper check: re-read one existing physical item without changing Desktop.
        bool? partialPassed=null;
        if(passed && expected.FirstOrDefault() is {} sample && executable!=null)
        {
            var partial=DesktopScanProcess.ReadAsync(executable,false,true,CancellationToken.None,paths:[sample],raw:true)
                .WaitAsync(TimeSpan.FromSeconds(35)).GetAwaiter().GetResult();
            partialPassed=partial.Count==1 && string.Equals(partial[0].Path,sample,StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(partial[0].GroupIdentity);
            ReadPartial(scanner,sample);
            partialPassed &= scanner.ScanState.Error==null && scanner.GetIcons().Count==icons.Count;
            passed &= partialPassed==true;
        }
        var report = new { Passed = passed, Seconds = clock.Elapsed.TotalSeconds, Count = icons.Count,
            PartialReadPassed=partialPassed,
            Scan = scanner.ScanState, Diagnostic = scanner.ScanFailureDiagnostic, Missing = missing,
            Items = icons.Select(i => new { i.Name, i.Path, i.IsSystemIcon, i.SourcePaths }).ToArray() };
        File.WriteAllText(Path.Combine(output, "desktop-discovery.json"), JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Desktop discovery: {(passed ? "PASS" : "FAIL")}; {icons.Count} items; {missing.Length} missing physical entries.");
        return passed ? 0 : 1;
    }
    private static void ReadPartial(DesktopIconService scanner,string sample)=>scanner.ScanDesktopChangesAsync([sample])
        .WaitAsync(TimeSpan.FromSeconds(35)).GetAwaiter().GetResult();
}
