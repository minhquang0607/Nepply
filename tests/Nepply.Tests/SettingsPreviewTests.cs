using System.Windows;
using System.Windows.Controls;
using Nepply;
using Nepply.Models;
using Nepply.Views;

internal static class SettingsPreviewTests
{
    internal static void Run(Action<string, bool> check, Action<int> pump)
    {
        PreviewCommitTests.Run(check);
        StartupSettingsTests.Run(check);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int scans = 0;
        using (var catalog = new Nepply.Services.DesktopIconService(async (options, token) =>
        {
            if (Interlocked.Increment(ref scans) == 1) { entered.TrySetResult(); await release.Task.WaitAsync(token); }
            return new List<DesktopIcon> { new() { Name = options.ShowHiddenFiles ? "obsolete" : "restored", Path = "fixture" } };
        }))
        {
            var published = new List<string>();
            catalog.IconsChanged += () => published.Add(catalog.GetIcons()[0].Name);
            catalog.Configure(new(true, true, true));
            var scan = catalog.ScanDesktopIconsAsync();
            bool started = entered.Task.Wait(TimeSpan.FromSeconds(3));
            catalog.Configure(new(false, true, true)); release.TrySetResult();
            bool finished = scan.Wait(TimeSpan.FromSeconds(3));
            check("Cancelled catalog preview cannot publish obsolete scan", started && finished && scans == 2 && published.SequenceEqual(["restored"]));
        }
        int desktopScans = 0;
        var iconsSource = new Nepply.Services.DesktopIconService((options, token) =>
        { Interlocked.Increment(ref desktopScans); return Task.FromResult(Enumerable.Range(0, options.ShowHiddenFiles ? 210 : 200).Select(i => new DesktopIcon { Name = "Item " + i, Path = "fixture-" + i }).ToList()); });
        iconsSource.ScanDesktopIconsAsync().GetAwaiter().GetResult();
        var monitor = new PreviewMonitor();
        var panel = new MainWindow(preview: true, monitor: monitor, icons: iconsSource) { Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
        SettingsWindow? settings = null;
        try
        {
            panel.Show(); pump(30);
            var baseline = panel.Settings.Clone();
            var icons = (IconPanel)panel.FindName("IconPanelView");
            icons.GridOffset = 200; panel.UpdateLayout();
            double originalOffset = icons.GridOffset;
            var grid = (IconGridPanel)typeof(IconPanel).GetMethod("FindGrid", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, new[] { icons.FindName("IconList") })!;
            int originalAnchor = (int)(originalOffset / grid.RowHeight) * grid.ColumnCount;
            var originalAccent = ((System.Windows.Media.SolidColorBrush)Application.Current.Resources["AccentBrush"]).Color;
            settings = new SettingsWindow(panel.Settings) { Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
            settings.PreviewChanged += panel.PreviewSettings;
            settings.Show(); pump(30);
            int configureCalls = monitor.Calls;
            var width = (Slider)settings.FindName("WidthSlider");
            var contentGrid = (Grid)panel.FindName("ContentGrid");
            var originalRow = contentGrid.RowDefinitions[0];
            int previews = 0; settings.PreviewChanged += _ => previews++;
            var usable = Nepply.Modules.Panel.PanelDimensions.Available(Nepply.Helpers.ScreenManager.Select(baseline.MonitorDevice));
            for (int i = 0; i < 100; i++) width.Value = (340 + i) / usable.Width * 100;
            width.Value = 520 / usable.Width * 100;
            pump(100);
            check("Slider burst coalesces to one preview", previews == 1);
            check("Dimension preview retains existing layout definitions", ReferenceEquals(originalRow, contentGrid.RowDefinitions[0]));
            check("Dimension slider endpoint is relative 100 percent", width.Maximum == 100);
            var picker = (DotGridPicker)settings.FindName("GridPicker");
            picker.Columns = 3; picker.Rows = 4;
            settings.FlushPreview();
            check("Preview changes panel geometry and grid", Math.Abs(panel.Width - 520) < 1 && icons.GridColumns == 3 && icons.GridRows == 4);
            check("Grid preview does not wake telemetry providers", monitor.Calls == configureCalls);
            check("Grid preview does not rescan Desktop", desktopScans == 1);
            check("Grid preview retains panel rows without rebuilding monitor", ReferenceEquals(originalRow, contentGrid.RowDefinitions[0]));
            var anchored = grid.SlotBounds(originalAnchor);
            check("Grid resize retains original visible item", anchored.Bottom > 0 && anchored.Top < grid.ViewportHeight);
            var rowBeforeAppearance = contentGrid.RowDefinitions[0];
            ((ComboBox)settings.FindName("AccentChoice")).SelectedItem = "Neon Cyan"; settings.FlushPreview();
            check("Appearance preview retains existing layout definitions", ReferenceEquals(rowBeforeAppearance, contentGrid.RowDefinitions[0]));
            check("Color preview changes rendered resources", ((System.Windows.Media.SolidColorBrush)Application.Current.Resources["AccentBrush"]).Color != originalAccent);
            ((CheckBox)settings.FindName("MetallicEnabled")).IsChecked = false; settings.FlushPreview();
            check("Material toggle previews without saving", !MetallicSurface.MaterialEnabled && panel.Settings.MetallicEnabled == baseline.MetallicEnabled);
            ((ComboBox)settings.FindName("MotionChoice")).SelectedIndex = (int)MotionMode.Off; settings.FlushPreview();
            check("Motion preview is global without saving", !Nepply.Modules.Appearance.MotionPolicy.Enabled && panel.Settings.Motion == baseline.Motion);
            var network = (ComboBox)settings.FindName("NetworkChoice");
            network.ItemsSource = new[] { new SettingsWindow.Choice("fixture-network", "Fixture network") }; network.SelectedValue = "fixture-network"; settings.FlushPreview();
            check("Network preview applies without changing sensor permissions", monitor.Last.NetworkInterfaceId == "fixture-network" && monitor.Last.HardwareSensorsEnabled == baseline.HardwareSensorsEnabled && panel.Settings.NetworkInterfaceId == baseline.NetworkInterfaceId);
            ((CheckBox)settings.FindName("ShowNetwork")).IsChecked = false; settings.FlushPreview();
            check("Network card off preview reaches collector without committing", !monitor.Last.ShowNetwork && panel.Settings.ShowNetwork == baseline.ShowNetwork);
            ((CheckBox)settings.FindName("SuspendMonitor")).IsChecked = false; settings.FlushPreview();
            check("Sleep preference previews without committing", !monitor.Last.SuspendMonitorWhenHidden && panel.Settings.SuspendMonitorWhenHidden == baseline.SuspendMonitorWhenHidden);
            ((CheckBox)settings.FindName("HiddenFiles")).IsChecked = true; settings.FlushPreview(); pump(150);
            check("Desktop filter preview refreshes catalog without saving", iconsSource.GetIcons().Count == 210 && !panel.Settings.ShowHiddenFiles);
            check("Preview never mutates committed configuration", panel.Settings.PanelWidth == baseline.PanelWidth && panel.Settings.GridColumns == baseline.GridColumns);
            ((CheckBox)settings.FindName("AutoStart")).IsChecked = !baseline.AutoStart;
            ((CheckBox)settings.FindName("HideIcons")).IsChecked = !baseline.HideDesktopIcons;
            settings.FlushPreview();
            check("Preview keeps Windows settings uncommitted", panel.Settings.AutoStart == baseline.AutoStart && panel.Settings.HideDesktopIcons == baseline.HideDesktopIcons);
            var edge = (ComboBox)settings.FindName("EdgeChoice");
            edge.SelectedIndex = (int)EdgePosition.Top;
            check("Top grid starts independently", picker.Columns == 6 && picker.Rows == 1);
            picker.Columns = 5; picker.Rows = 2; settings.FlushPreview();
            check("Top grid preview reaches panel", icons.GridColumns == 5 && icons.GridRows == 2);
            edge.SelectedIndex = (int)EdgePosition.Left;
            check("Switching edge restores side draft", picker.Columns == 3 && picker.Rows == 4);
            edge.SelectedIndex = (int)EdgePosition.Top;
            check("Switching edge retains top draft", picker.Columns == 5 && picker.Rows == 2);
            edge.SelectedIndex = (int)EdgePosition.Bottom; settings.FlushPreview();
            check("Bottom choice preserves horizontal grid", edge.Items.Count == 4 && picker.Columns == 5 && picker.Rows == 2 && icons.GridRows == 2);
            var screen = Nepply.Helpers.ScreenManager.Select(baseline.MonitorDevice);
            var physical = panel.PointToScreen(new System.Windows.Point());
            check("Bottom preview anchors above work-area bottom", Math.Abs(physical.Y + panel.ActualHeight * screen.Scale - (screen.Bottom - Math.Round(12 * screen.Scale))) <= 2);
            check("Bottom preview does not commit edge", panel.Settings.EdgePosition == baseline.EdgePosition);
            settings.Close(); panel.EndSettingsPreview(); pump(30);
            check("Cancel restores cached Desktop membership", iconsSource.GetIcons().Count == 200);
            check("Cancel restores panel grid and width", Math.Abs(panel.Width - baseline.PanelWidth) < 1 && icons.GridColumns == baseline.GridColumns && icons.GridRows == baseline.GridRows);
            check("Cancel restores original scroll offset", originalOffset > 0 && Math.Abs(icons.GridOffset - originalOffset) < 1);
            check("Cancel restores palette and network", ((System.Windows.Media.SolidColorBrush)Application.Current.Resources["AccentBrush"]).Color == originalAccent && monitor.Last.NetworkInterfaceId == baseline.NetworkInterfaceId);
            check("Cancel restores material preference", MetallicSurface.MaterialEnabled == baseline.MetallicEnabled);
            check("Cancel restores motion mode", Nepply.Modules.Appearance.MotionPolicy.Mode == baseline.Motion);
            check("Cancel restores monitor sleep preference", monitor.Last.SuspendMonitorWhenHidden == baseline.SuspendMonitorWhenHidden);
            check("Cancel restores network sampling preference", monitor.Last.ShowNetwork == baseline.ShowNetwork);
            width.Value = 560; pump(100);
            check("Closed settings cannot publish queued preview", Math.Abs(panel.Width - baseline.PanelWidth) < 1);
        }
        finally { settings?.Close(); panel.EndSettingsPreview(); panel.Close(); }
    }
    private sealed class PreviewMonitor : Nepply.Services.Telemetry.IHardwareMonitor
    {
        public int Calls; public AppSettings Last = new();
        public event Action<HardwareInfoSnapshot>? HardwareUpdated { add { } remove { } }
        public HardwareInfoSnapshot GetSnapshot() => HardwareInfoSnapshot.Empty;
        public void Configure(AppSettings settings, bool visible) { Calls++; Last = settings.Clone(); }
        public void Start() { }
        public void Dispose() { }
    }
}
