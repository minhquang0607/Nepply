using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Nepply;
using Nepply.Models;
using Nepply.Modules.Desktop;
using Nepply.Services;
using Nepply.Views;

internal static class NativeIconGridTests
{
    internal static int Run(string output, bool mixed = false)
    {
        string root = Path.Combine(output, "native-icons-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        for (int i = 0; i < (mixed ? 200 : 500); i++) File.WriteAllText(Path.Combine(root, $"Mẫu-日本語-{i:D3}.txt"), "fixture");
        if (mixed)
        {
            for (int i = 0; i < 100; i++)
            {
                Directory.CreateDirectory(Path.Combine(root, $"Thư mục-日本語-{i:D3}"));
                File.WriteAllText(Path.Combine(root, $"Liên kết-{i:D3}.url"), "[InternetShortcut]\r\nURL=https://example.invalid/fixture\r\n");
            }
            ShellDesktopReader.RunSta(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    object link = new Nepply.Helpers.NativeInterop.ShellLink();
                    try
                    {
                        ((Nepply.Helpers.NativeInterop.IShellLinkW)link).SetPath(Path.Combine(root, $"Mẫu-日本語-{i:D3}.txt"));
                        ((System.Runtime.InteropServices.ComTypes.IPersistFile)link).Save(Path.Combine(root, $"Lối tắt-日本語-{i:D3}.lnk"), true);
                    }
                    finally { Marshal.ReleaseComObject(link); }
                }
                return true;
            }).WaitAsync(TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();
        }
        var clock = Stopwatch.StartNew();
        // Exercise the production folder reader without relaxing Desktop path guards.
        var items = ShellDesktopReader.RunSta(() =>
        {
            var result = new List<DesktopIcon>();
            long budget = DesktopImageBudget.MaximumDecodedBytes;
            DesktopEntryReader.ScanFolder(root, result, [], null, new DesktopScanOptions(), ref budget, File.GetAttributes, new DesktopScanIssues());
            foreach (var item in result) item.GroupIdentity = ShellDesktopReader.DestinationIdentity(item);
            return result;
        }).WaitAsync(TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();
        double scanSeconds = clock.Elapsed.TotalSeconds;
        bool nativeValid = items.Count == 500 && items.All(i => i.Icon is BitmapSource { IsFrozen: true })
            && items.Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(Directory.GetFileSystemEntries(root));
        bool labelsValid = items.All(i => i.Name == (Path.GetExtension(i.Path) is ".lnk" or ".url" ? Path.GetFileNameWithoutExtension(i.Path) : Path.GetFileName(i.Path)));
        bool linksResolved = !mixed || items.Count(i => i.GroupIdentity?.StartsWith("link:", StringComparison.Ordinal) == true) == 100;
        var app = new App(testHost: true); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        using var catalog = new DesktopIconService(_ => Task.FromResult(items));
        catalog.ScanDesktopIconsAsync().GetAwaiter().GetResult();
        var window = new MainWindow(preview: true, icons: catalog, monitor: new QuietMonitor()) { ShowActivated = false, ShowInTaskbar = false };
        int displayed = 0; bool lastRealized = false;
        try
        {
            window.Settings.ShowMonitor = false;
            window.Show(); Pump(); window.UpdateLayout();
            var panel = (IconPanel)window.FindName("IconPanelView");
            var list = (ListBox)panel.FindName("IconList");
            displayed = list.Items.Count;
            if (displayed > 0)
            {
                list.ScrollIntoView(list.Items[displayed - 1]); Pump(); list.UpdateLayout();
                lastRealized = list.ItemContainerGenerator.ContainerFromIndex(displayed - 1) != null;
            }
        }
        finally { window.Cleanup(); window.Close(); app.MainWindow = null; }
        bool passed = nativeValid && labelsValid && linksResolved && displayed == 500 && lastRealized;
        File.WriteAllText(Path.Combine(output, "native-icon-grid.json"), JsonSerializer.Serialize(new
        {
            Passed = passed,
            Mixed = mixed,
            NativeValid = nativeValid,
            LabelsValid = labelsValid,
            LinksResolved = linksResolved,
            ScanSeconds = scanSeconds,
            Displayed = displayed,
            LastRealized = lastRealized,
            Scope = (mixed ? "200 text files, 100 directories, 100 local .lnk, 100 .url" : "500 text files") + " with Unicode names in isolated folder; production ScanFolder/Shell icon extraction and shortcut identity on STA, frozen bitmaps, WPF catalog binding and scroll-to-last realization. No link execution. Catalog input is injected; no Desktop namespace enumeration, helper IPC, watcher pipeline, UNC, custom icons or long-run leak verdict."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Native icons={nativeValid}; grid={displayed}; last realized={lastRealized}; scan={scanSeconds:F2}s");
        return passed ? 0 : 1;
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(250) };
        EventHandler handler = (_, _) => frame.Continue = false;
        timer.Tick += handler; timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); timer.Tick -= handler; }
    }
    private sealed class QuietMonitor : Nepply.Services.Telemetry.IHardwareMonitor
    {
        public event Action<HardwareInfoSnapshot>? HardwareUpdated { add { } remove { } }
        public HardwareInfoSnapshot GetSnapshot() => HardwareInfoSnapshot.Empty;
        public void Configure(AppSettings settings, bool visible) { }
        public void Start() { }
        public void Dispose() { }
    }
}
