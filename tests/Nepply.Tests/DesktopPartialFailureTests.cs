using System.IO;
using System.Runtime.InteropServices;
using Nepply.Models;
using Nepply.Modules.Desktop;
using Nepply.Services;

/// <summary>
/// Real files on a real STA read, with one entry made to fail. Ordinary odd files (denied ACL, locked,
/// device names, dangling junction) do not make File.GetAttributes throw, so the failure is injected.
/// </summary>
internal static class DesktopPartialFailureTests
{
    private sealed class Fixture
    {
        internal readonly string User, Public;
        internal readonly Dictionary<string, Exception> Broken = new(StringComparer.OrdinalIgnoreCase);
        internal List<NativeDesktopNamespace.Entry> Shell = [];
        internal Exception? ShellFailure;
        internal string? LoseUserOnRead;
        internal readonly List<string[]?> Requests = [];
        private readonly string _hiddenUser;

        internal Fixture(string root)
        {
            string home = Path.Combine(root, "partial-" + Guid.NewGuid().ToString("N"));
            User = Path.Combine(home, "user"); Public = Path.Combine(home, "public"); _hiddenUser = User + "-lost";
            Directory.CreateDirectory(User); Directory.CreateDirectory(Public);
        }
        internal string Create(string folder, string name)
        {
            string path = Path.Combine(folder, name); File.WriteAllText(path, "fixture"); return path;
        }
        internal string Break(string path, Exception? error = null) { Broken[path] = error ?? new IOException("Fixture entry fault"); return path; }
        internal void LoseUser() => Directory.Move(User, _hiddenUser);
        internal void RestoreUser() { LoseUserOnRead = null; if (Directory.Exists(_hiddenUser)) Directory.Move(_hiddenUser, User); }
        private FileAttributes Attributes(string path)
        {
            if (string.Equals(path, LoseUserOnRead, StringComparison.OrdinalIgnoreCase)) { LoseUser(); throw new IOException("Fixture source lost mid-read"); }
            return Broken.TryGetValue(path, out var error) ? throw error : File.GetAttributes(path);
        }
        internal DesktopReadSource Source => new([User, Public], (_, _) => ShellFailure != null ? throw ShellFailure : Shell, Attributes);
        internal List<DesktopIcon> Read(string[]? paths, DesktopScanIssues issues, DesktopScanOptions? options = null) =>
            ShellDesktopReader.RunSta(() => DesktopEntryReader.ReadNative(options ?? new DesktopScanOptions(), paths, Source, issues))
                .WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        internal DesktopIconService Service() => new((options, paths, _) =>
        {
            Requests.Add(paths);
            return ShellDesktopReader.RunSta(() => DesktopEntryReader.ReadNative(options, paths, Source, new DesktopScanIssues()));
        }, () => [User, Public]);
    }

    private static string[] Paths(IEnumerable<DesktopIcon> items) => items.Select(i => i.Path).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    private static bool Same(IEnumerable<DesktopIcon> items, params string[] paths) =>
        Paths(items).SequenceEqual(paths.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
    private static void Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
    private static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); return false; }
        catch (T) { return true; }
    }

    internal static void Run(string root, Action<string, bool> check)
    {
        void Scenario(string name, Action body)
        {
            // A read that aborts must fail its own checks, not stop the whole test run.
            try { body(); }
            catch (Exception ex) { check("Partial failure scenario completes: " + name + ": " + ex.Message, false); }
        }
        Scenario("one bad entry", () => OneBadEntry(root, check));
        Scenario("logging", () => BadEntryIsLogged(root, check));
        Scenario("source loss", () => SourceLoss(root, check));
        Scenario("every entry fails", () => EveryEntryFails(root, check));
        Scenario("partial read", () => PartialRead(root, check));
        Scenario("Shell items", () => ShellItems(root, check));
        Scenario("enumerator", () => Enumerator(check));
    }

    private static void OneBadEntry(string root, Action<string, bool> check)
    {
        foreach (var fault in new Exception[] { new IOException("fixture"), new UnauthorizedAccessException("fixture"), new COMException("fixture", unchecked((int)0x80004005)) })
        {
            var f = new Fixture(root);
            string a = f.Create(f.User, "a.txt"), bad = f.Break(f.Create(f.User, "bad.txt"), fault), c = f.Create(f.User, "c.txt"), p = f.Create(f.Public, "p.txt");
            var issues = new DesktopScanIssues();
            var items = f.Read(null, issues);
            check("One bad Desktop entry leaves the others readable: " + fault.GetType().Name, Same(items, a, c, p) && issues.Count == 1);
        }
        var fixture = new Fixture(root);
        string first = fixture.Create(fixture.User, "first.txt"), broken = fixture.Break(fixture.Create(fixture.User, "broken.txt")), pub = fixture.Create(fixture.Public, "pub.txt");
        using (var service = fixture.Service())
        {
            Wait(service.ScanDesktopIconsAsync());
            check("First scan with one bad entry publishes the others without an error", Same(service.GetIcons(), first, pub) && service.HasScanned && service.ScanState.Error == null);
        }
        var alone = new Fixture(root);
        alone.Break(alone.Create(alone.User, "only.txt"));
        string other = alone.Create(alone.Public, "other.txt");
        var loneIssues = new DesktopScanIssues();
        check("A folder whose only entry is bad does not block the other folder", Same(alone.Read(null, loneIssues), other) && loneIssues.Count == 1);
        var over = new Fixture(root);
        over.Create(over.User, "one-too-many.txt");
        var full = Enumerable.Range(0, DesktopImageBudget.MaximumEntries).Select(_ => new DesktopIcon()).ToList();
        var budgetIssues = new DesktopScanIssues();
        bool budgetAborts = false;
        try
        {
            long pixels = 0;
            DesktopEntryReader.ScanFolder(over.User, full, [], null, new DesktopScanOptions(), ref pixels, File.GetAttributes, budgetIssues);
        }
        catch (IOException ex) when (ex.InnerException is InvalidDataException) { budgetAborts = true; }
        check("Item budget still aborts the read instead of skipping entries", budgetAborts && budgetIssues.Count == 0);
    }

    private static void BadEntryIsLogged(string root, Action<string, bool> check)
    {
        var f = new Fixture(root);
        f.Create(f.User, "fine.txt");
        string bad = f.Break(f.Create(f.User, "logged-" + Guid.NewGuid().ToString("N") + ".txt"));
        f.Read(null, new DesktopScanIssues());
        check("Skipped Desktop entry reaches the log with its path and error", LogContains(bad, "Fixture entry fault"));
        var many = new DesktopScanIssues();
        string marker = "bounded-" + Guid.NewGuid().ToString("N");
        for (int i = 0; i < DesktopScanIssues.MaximumLogged + 5; i++) many.Skip(marker + "-" + i + "-", new IOException("fixture"));
        check("One scan logs a bounded number of skipped entries", many.Count == DesktopScanIssues.MaximumLogged + 5
            && LogContains(marker + "-" + (DesktopScanIssues.MaximumLogged - 1) + "-") && !LogContains(marker + "-" + DesktopScanIssues.MaximumLogged + "-", wait: false));
    }

    private static bool LogContains(string first, string? second = null, bool wait = true)
    {
        // Other Nepply processes on this machine may share the file, so read without blocking them.
        string file = Path.Combine(AppLogger.LogDirectory, "nepply-ui.log");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        do
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                string text = reader.ReadToEnd();
                if (text.Contains(first, StringComparison.Ordinal) && (second == null || text.Contains(second, StringComparison.Ordinal))) return true;
            }
            catch (IOException) { }
            if (wait) Thread.Sleep(50);
        } while (wait && clock.Elapsed < TimeSpan.FromSeconds(5));
        return false;
    }

    private static void SourceLoss(string root, Action<string, bool> check)
    {
        var f = new Fixture(root);
        string a = f.Create(f.User, "a.txt"), b = f.Create(f.User, "b.txt"), p = f.Create(f.Public, "p.txt");
        using var service = f.Service();
        Wait(service.ScanDesktopIconsAsync());
        var snapshot = service.GetIcons();
        check("Source loss fixture starts complete", Same(snapshot, a, b, p) && service.ScanState.Error == null);

        f.LoseUserOnRead = a;
        Wait(service.ScanDesktopIconsAsync());
        check("Source lost during a read keeps the last snapshot", ReferenceEquals(snapshot, service.GetIcons()) && service.ScanState.Error == "DesktopRefreshError");
        f.RestoreUser();
        Wait(service.ScanDesktopIconsAsync());
        check("Next scan recovers once the source returns", Same(service.GetIcons(), a, b, p) && service.ScanState.Error == null);

        snapshot = service.GetIcons();
        f.LoseUser();
        Wait(service.ScanDesktopIconsAsync());
        check("Source missing before a read keeps the last snapshot", ReferenceEquals(snapshot, service.GetIcons()) && service.ScanState.Error == "DesktopRefreshError");
        f.RestoreUser();

        Wait(service.ScanDesktopIconsAsync());
        snapshot = service.GetIcons();
        f.ShellFailure = new COMException("Fixture Shell lost", unchecked((int)0x800706BA));
        Wait(service.ScanDesktopIconsAsync());
        check("Shell namespace lost keeps the last snapshot", ReferenceEquals(snapshot, service.GetIcons()) && service.ScanState.Error == "DesktopRefreshError");
        f.ShellFailure = null;

        var fresh = new Fixture(root);
        fresh.Create(fresh.User, "a.txt");
        fresh.LoseUser();
        using var first = fresh.Service();
        Wait(first.ScanDesktopIconsAsync());
        check("First scan with a lost source reports failure, not an empty Desktop", !first.HasScanned && first.ScanState.Error == "DesktopReadError" && first.GetIcons().Count == 0);
    }

    private static void EveryEntryFails(string root, Action<string, bool> check)
    {
        var f = new Fixture(root);
        string a = f.Create(f.User, "a.txt"), b = f.Create(f.User, "b.txt"), p = f.Create(f.Public, "p.txt");
        using var service = f.Service();
        Wait(service.ScanDesktopIconsAsync());
        var snapshot = service.GetIcons();
        f.Break(a); f.Break(b);
        Wait(service.ScanDesktopIconsAsync());
        check("Every entry of a folder failing keeps the last snapshot", ReferenceEquals(snapshot, service.GetIcons()) && service.ScanState.Error == "DesktopRefreshError");

        var lone = new Fixture(root);
        lone.Break(lone.Create(lone.User, "only.txt"));
        check("Nothing readable with a failure is not an empty Desktop", Throws<IOException>(() => lone.Read(null, new DesktopScanIssues())));
        var empty = new Fixture(root);
        check("A truly empty Desktop still reads as empty", empty.Read(null, new DesktopScanIssues()).Count == 0);
    }

    private static void PartialRead(string root, Action<string, bool> check)
    {
        var f = new Fixture(root);
        string a = f.Create(f.User, "a.txt"), b = f.Create(f.User, "b.txt"), p = f.Create(f.Public, "p.txt");
        using var service = f.Service();
        Wait(service.ScanDesktopIconsAsync());
        var untouched = service.GetIcons().Single(i => i.Path == p);

        string added = f.Create(f.User, "added.txt"), bad = f.Break(f.Create(f.User, "bad.txt"));
        Wait(service.ScanDesktopChangesAsync([added, bad]));
        check("Partial read with one bad entry publishes the rest", f.Requests[^1]?.Length == 2 && Same(service.GetIcons(), a, b, p, added) && service.ScanState.Error == null
            && ReferenceEquals(untouched, service.GetIcons().Single(i => i.Path == p)));
        File.WriteAllText(a, "changed");
        Wait(service.ScanDesktopChangesAsync([a]));
        check("Baseline stays partial after a skipped entry", f.Requests[^1] is [var only] && only == a && service.ScanState.Error == null);

        var snapshot = service.GetIcons();
        string second = f.Break(f.Create(f.User, "bad2.txt"));
        Wait(service.ScanDesktopChangesAsync([bad, second]));
        check("Every changed entry failing keeps the last snapshot", f.Requests[^1]?.Length == 2 && ReferenceEquals(snapshot, service.GetIcons()) && service.ScanState.Error == "DesktopRefreshError");
        Wait(service.ScanDesktopChangesAsync([a]));
        check("Next request repairs with a full read after that failure", f.Requests[^1] == null && service.ScanState.Error == null && Same(service.GetIcons(), a, b, p, added));

        snapshot = service.GetIcons();
        f.LoseUserOnRead = a;
        Wait(service.ScanDesktopChangesAsync([a]));
        check("Partial read losing its source keeps the last snapshot", ReferenceEquals(snapshot, service.GetIcons()) && service.ScanState.Error == "DesktopRefreshError");
        f.RestoreUser();
    }

    private static void ShellItems(string root, Action<string, bool> check)
    {
        var f = new Fixture(root);
        string a = f.Create(f.User, "a.txt"), bad = f.Break(f.Create(f.User, "bad.txt")), c = f.Create(f.User, "c.txt");
        f.Shell = [new(a, "a.txt"), new(bad, "bad.txt"), new(c, "c.txt")];
        var issues = new DesktopScanIssues();
        var items = ShellDesktopReader.ReadFrom(f.Source, false, false, issues);
        check("Shell filter skips one bad item and keeps the others", Same(items, a, c) && issues.Count == 1);
    }

    private static void Enumerator(Action<string, bool> check)
    {
        static NativeDesktopNamespace.Entry Good(IntPtr child) => new("path", "name");
        static IntPtr Alloc() => Marshal.AllocCoTaskMem(1);
        const int End = -1, NoChild = -2, Fail = unchecked((int)0x80004005);
        NativeDesktopNamespace.NextChild Script(params int[] steps)
        {
            int index = 0;
            return (out IntPtr child, out uint fetched) =>
            {
                int step = steps[index++];
                child = step is 1 or 7 ? Alloc() : IntPtr.Zero;
                fetched = step is End or Fail ? 0u : step == 7 ? 2u : 1u;
                return step == Fail ? Fail : step == End ? 1 : 0;
            };
        }
        // 1 is a child; 7 reports two children at once; NoChild is a null child with fetched 1.
        var issues = new DesktopScanIssues();
        int read = 0;
        var kept = NativeDesktopNamespace.Collect(Script(1, NoChild, 1, End), child => { read++; return Good(child); }, issues);
        check("Null Shell child is skipped, later children are read", kept.Count == 2 && read == 2 && issues.Count == 1);

        issues = new DesktopScanIssues();
        read = 0;
        kept = NativeDesktopNamespace.Collect(Script(1, 1, 1, End), child => ++read == 2 ? throw new COMException("Fixture extension", Fail) : Good(child), issues);
        check("Shell item that cannot be read is skipped, later items are read", kept.Count == 2 && read == 3 && issues.Count == 1);

        issues = new DesktopScanIssues();
        kept = NativeDesktopNamespace.Collect(Script(1, End), _ => throw new COMException("Fixture extension", Fail), issues);
        check("Single bad Shell item does not fail the namespace", kept.Count == 0 && issues.Count == 1);

        check("Every Shell item failing fails the namespace", Throws<IOException>(() =>
            NativeDesktopNamespace.Collect(Script(1, 1, End), _ => throw new COMException("Fixture extension", Fail), new DesktopScanIssues())));
        check("Enumerator failure mid-list stays a source failure", Throws<COMException>(() =>
            NativeDesktopNamespace.Collect(Script(1, Fail), Good, new DesktopScanIssues())));
        check("Invalid fetched count stays a source failure", Throws<IOException>(() =>
            NativeDesktopNamespace.Collect(Script(7), Good, new DesktopScanIssues())));

        int calls = 0;
        bool bounded = Throws<IOException>(() => NativeDesktopNamespace.Collect((out IntPtr child, out uint fetched) =>
        {
            calls++; child = IntPtr.Zero; fetched = 1; return 0;
        }, Good, new DesktopScanIssues()));
        check("Enumerator returning only bad children still ends", bounded && calls == DesktopImageBudget.MaximumEntries + 1);
    }
}
