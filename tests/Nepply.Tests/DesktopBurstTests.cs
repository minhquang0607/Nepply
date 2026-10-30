using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Nepply.Models;
using Nepply.Services;

internal static class DesktopBurstTests
{
    internal static int Run(string output)
    {
        // Only mutate files created by this invocation. Keep artifacts for inspection.
        string root = Path.Combine(output, "burst-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var checks = new List<object>();
        var tasks = new ConcurrentBag<Task>();
        int scans = 0, fullScans = 0, publications = 0, failures = 0;
        using var service = new DesktopIconService((options, paths, token) =>
        {
            token.ThrowIfCancellationRequested();
            Interlocked.Increment(ref scans);
            if (paths == null) Interlocked.Increment(ref fullScans);
            var entries = (paths ?? Directory.GetFiles(root)).Where(File.Exists)
                .Select(path => new DesktopIcon { Path = path, Name = Path.GetFileName(path), GroupIdentity = path }).ToList();
            return Task.FromResult(entries);
        }, () => [root]);
        service.IconsChanged += () => Interlocked.Increment(ref publications);
        using var watcher = new DesktopWatcherService(() => { }, () => [root], scanChanges: paths => tasks.Add(service.ScanDesktopChangesAsync(paths)));
        void Check(string name, bool passed, double seconds = 0)
        {
            checks.Add(new { Name = name, Passed = passed, Seconds = seconds });
            if (!passed) failures++;
            Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name} ({seconds:F2}s)");
        }
        bool MatchesDisk()
        {
            var actual = service.GetIcons().Select(item => item.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return actual.SetEquals(Directory.GetFiles(root));
        }
        void Converge(string name)
        {
            var clock = Stopwatch.StartNew();
            bool matched = SpinWait.SpinUntil(() => !service.ScanState.IsScanning && MatchesDisk(), 15000);
            // A momentary match cannot hide a late stale publication.
            if (matched) { Thread.Sleep(1000); matched = MatchesDisk() && service.ScanState.Error == null; }
            Check(name, matched, clock.Elapsed.TotalSeconds);
        }
        try
        {
            watcher.ReconcileAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Check("Initial real watcher baseline", SpinWait.SpinUntil(() => service.HasScanned, 5000));
            for (int i = 0; i < 500; i++) File.WriteAllText(Path.Combine(root, $"item-{i:D3}.txt"), "fixture");
            Converge("500 real creates converge to disk membership");
            for (int i = 0; i < 200; i++) File.Move(Path.Combine(root, $"item-{i:D3}.txt"), Path.Combine(root, $"đổi-日本語-{i:D3}.txt"));
            Converge("200 real Unicode renames remove old identities");
            for (int i = 200; i < 450; i++) File.Delete(Path.Combine(root, $"item-{i:D3}.txt"));
            Converge("250 real deletes converge to remaining 250 items");
            Check("Final catalog has 250 unique items", service.GetIcons().Count == 250 && MatchesDisk());
            Thread.Sleep(1000);
            int quietScans = Volatile.Read(ref scans);
            Thread.Sleep(1500);
            Check("Quiet filesystem does not keep rescanning", Volatile.Read(ref scans) == quietScans);
            Check("Burst events are coalesced, not one scan per operation", scans < 950);
        }
        finally
        {
            watcher.Dispose();
            watcher.Completion.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Task.WhenAll(tasks.ToArray()).WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
        }
        File.WriteAllText(Path.Combine(output, "desktop-burst.json"), JsonSerializer.Serialize(new
        {
            Passed = failures == 0,
            Scans = scans,
            FullScans = fullScans,
            Publications = publications,
            Checks = checks,
            Scope = "Real FileSystemWatcher + debounce + DesktopIconService reconciliation; fixture file reader, no Shell icon extraction or WPF. No forced OS buffer overflow."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return failures == 0 ? 0 : 1;
    }
}
