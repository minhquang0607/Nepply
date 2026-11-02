using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using Nepply.Models;
using Nepply.Services;
using Nepply.ViewModels;
using Nepply.Views;
using Nepply.Modules.Desktop;

/// <summary>Logic and OS-metric checks that need no WPF application; they run in every suite.</summary>
internal static class CoreLogicTests
{
    internal static void Run(Action<string, bool> check)
    {
        check("Rate uses actual elapsed time", SystemMetrics.CalculateRate(4096, 0, 2) == 2048);
        check("Reset counter invalidates rate", SystemMetrics.CalculateRate(100, 200, 1) == null);
        check("Resume gap invalidates rate", SystemMetrics.CalculateRate(4096, 0, 90) == null);
        check("Zero interval invalidates rate", SystemMetrics.CalculateRate(4096, 0, 0) == null);
        check("NaN interval invalidates rate", SystemMetrics.CalculateRate(4096, 0, double.NaN) == null);
        check("Missing sensor remains missing", MetricValue.Valid(null, 0, 100) == null);
        check("NaN sensor rejected", MetricValue.Valid(float.NaN, 0, 100) == null);
        check("Infinity sensor rejected", MetricValue.Valid(float.PositiveInfinity, 0, 100) == null);
        check("Zero RPM valid", MetricValue.Valid(0, 0, 30000) == 0);
        check("Invalid temperature rejected", MetricValue.Valid(0, 1, 130) == null);
        check("Missing network not formatted as zero", HardwareMonitorViewModel.FormatSpeed(null) == "—");
        check("Speed units binary", HardwareMonitorViewModel.FormatSpeed(1048576).Contains("MiB/s"));
        var settings = new AppSettings { PanelWidth = -5, Theme = (ThemeOption)99, PanelOpacity = double.NaN, MonitorUpdateIntervalMs = 0 };
        settings.Validate();
        check("Settings bounds and invalid enums", settings.PanelWidth == 320 && settings.Theme == ThemeOption.System && settings.PanelOpacity == 0.98 && settings.MonitorUpdateIntervalMs == 1000);
        var shiftOnly = new AppSettings { HotkeyModifiers = 4, HotkeyKey = 32 }; shiftOnly.Validate();
        var altShift = new AppSettings { HotkeyModifiers = 5, HotkeyKey = 32 }; altShift.Validate();
        check("Saved hotkey without Ctrl or Alt falls back to the default", shiftOnly.HotkeyModifiers == 5 && altShift.HotkeyModifiers == 5);
        check("Every allowed hotkey modifier set includes Ctrl or Alt", new[] { 1, 2, 3, 5, 6, 7 }.All(value =>
            { var s = new AppSettings { HotkeyModifiers = value }; s.Validate(); return s.HotkeyModifiers == value; }));
        var oversized = new AppSettings
        {
            GpuIdentifier = new string('g', 2049),
            IntegratedGpuIdentifier = new string('i', 4096),
            MonitorDevice = new string('m', 5000),
            NetworkInterfaceId = new string('n', 3000)
        };
        oversized.Validate();
        var limitGpu = new string('g', AppSettings.MaximumIdentifierLength);
        var atLimit = new AppSettings { GpuIdentifier = limitGpu, NetworkInterfaceId = null! }; atLimit.Validate();
        check("Oversized device identifiers fall back to automatic selection", oversized.GpuIdentifier == "" && oversized.IntegratedGpuIdentifier == ""
            && oversized.MonitorDevice == "" && oversized.NetworkInterfaceId == "" && atLimit.GpuIdentifier == limitGpu && atLimit.NetworkInterfaceId == "");
        var copy = settings.Clone(); copy.PanelWidth = 500;
        check("Settings draft isolated", settings.PanelWidth == 320);
        var legacy = new AppSettings { PanelHeight = 520, GridCellWidth = double.NaN, GridCellHeight = double.PositiveInfinity }; legacy.Validate();
        check("Legacy layout migrates once", legacy.PanelHeight == 320 && legacy.LayoutVersion == 1);
        check("Invalid grid values normalized", legacy.GridCellWidth == 88 && legacy.GridCellHeight == 90);
        legacy.PanelHeight = 520; legacy.Validate();
        check("Custom height survives subsequent validation", legacy.PanelHeight == 520);
        check("Disk percentage derived", new DiskDriveInfo("C:", "System", 50, 200).UsagePercent == 25);
        var screen = new Nepply.Helpers.DisplayScreen("test", -1920, 0, 0, 1080, 1, false);
        check("Edge rectangle excludes adjacent display", screen.Contains(-1, 200) && !screen.Contains(0, 200) && !screen.Contains(-1921, 200));
        check("Snapshot collection immutable", typeof(HardwareInfoSnapshot).GetProperty("Drives")!.PropertyType == typeof(ImmutableArray<DiskDriveInfo>));

        var memory = SystemMetrics.ReadMemory();
        check("Windows RAM coherent", memory.Total > 0 && memory.Used >= 0 && memory.Used <= memory.Total && Math.Abs(memory.Load!.Value - memory.Used!.Value / memory.Total!.Value * 100) < 0.01);
        var monitor = new HardwareMonitorService(1000);
        monitor.Configure(new AppSettings { HardwareSensorsEnabled = false, EcoMode = false, MonitorUpdateIntervalMs = 1000 }, true);
        using var sampled = new ManualResetEventSlim();
        var snapshots = new List<HardwareInfoSnapshot>();
        monitor.HardwareUpdated += value => { lock (snapshots) { snapshots.Add(value); if (snapshots.Count >= 3) sampled.Set(); } };
        monitor.Start();
        check("Monitor publishes three frames", sampled.Wait(10000));
        lock (snapshots)
        {
            check("Sequences monotonic", snapshots.Zip(snapshots.Skip(1), (a, b) => b.Sequence > a.Sequence).All(x => x));
            check("Physical RAM total stable", snapshots.Count >= 3 && snapshots.Max(x => x.RamTotalGB) == snapshots.Min(x => x.RamTotalGB));
            check("Disabled native sensors remain unavailable", snapshots.All(x => x.Hardware.CpuTemp == null));
        }
        var stopwatch = Stopwatch.StartNew(); monitor.Dispose(); stopwatch.Stop();
        // A blocking dispose waits for a sampling cycle (1.5 s); 500 ms leaves room for slow CI runners.
        check("Dispose returns without UI wait", stopwatch.ElapsedMilliseconds < 500);
        monitor.Dispose(); monitor.Configure(new AppSettings(), true); monitor.Start();
        check("Repeated teardown and late configuration do not throw", true);
        using (var cold = new HardwareMonitorService()) { cold.Dispose(); cold.Start(); }
        check("Dispose before start is safe", true);
        TelemetryFreshnessTests.RunRecovery(check);
        using (var broken = new HardwareMonitorService(() => throw new IOException("Injected provider failure"), 1000))
        {
            broken.Configure(new AppSettings { EcoMode = false, MonitorUpdateIntervalMs = 1000 }, true);
            using var healthyWindows = new ManualResetEventSlim();
            broken.HardwareUpdated += frame => { if (frame.Sequence >= 3 && frame.Hardware.Error != null && frame.RamTotalGB > 0) healthyWindows.Set(); };
            broken.Start();
            check("Provider failure does not stop Windows metrics", healthyWindows.Wait(7000));
        }

        using (var cadence = new HardwareMonitorService())
        {
            cadence.Configure(new AppSettings { HardwareSensorsEnabled = false, EcoMode = false, MonitorUpdateIntervalMs = 10000 }, true);
            using var ready = new ManualResetEventSlim(); cadence.HardwareUpdated += _ => ready.Set(); cadence.Start();
            check("Slow cadence initializes immediately", ready.Wait(5000));
            long beforeChange = cadence.WindowsSampleCount;
            cadence.Configure(new AppSettings { HardwareSensorsEnabled = false, EcoMode = false, MonitorUpdateIntervalMs = 1000 }, true);
            check("Interval applies without restart", SpinWait.SpinUntil(() => cadence.WindowsSampleCount >= beforeChange + 2, 3500));
            long beforeHidden = cadence.WindowsSampleCount;
            cadence.Configure(new AppSettings { HardwareSensorsEnabled = false, EcoMode = true, SuspendMonitorWhenHidden = false, MonitorUpdateIntervalMs = 1000 }, false);
            check("Hidden configuration wakes sampler once", SpinWait.SpinUntil(() => cadence.WindowsSampleCount > beforeHidden, 3000));
            long hiddenCount = cadence.WindowsSampleCount;
            Thread.Sleep(1500);
            check("Hidden eco cadence does not retain one-second polling", cadence.WindowsSampleCount == hiddenCount);
        }

        check("Home namespace rejected", !ShellDesktopReader.IsDesktopSystemIcon("::{F874310E-B6B7-47DC-BC84-B9E6B38F5903}"));
        check("Libraries namespace rejected", !ShellDesktopReader.IsDesktopSystemIcon("::{031E4825-7B94-4DC3-B131-E946B44C8DD5}"));
        check("Unsupported AMD metric rejected", AmdTelemetry.ValidateMetric(0, 45, 1, 130) == null);
        check("Invalid AMD temperature rejected", AmdTelemetry.ValidateMetric(1, 0, 1, 130) == null);
        check("Supported AMD temperature retained", AmdTelemetry.ValidateMetric(1, 45, 1, 130) == 45);
        check("HSV red converts to RGB", ColorField.FromHsv(0, 1, 1) == Colors.Red);
        check("HSV hue wraps", ColorField.FromHsv(360, 1, 1) == Colors.Red);
        check("HSV white and black endpoints", ColorField.FromHsv(100, 0, 1) == Colors.White && ColorField.FromHsv(100, 1, 0) == Colors.Black);
        var layoutDraft = new AppSettings(); var layoutClone = layoutDraft.Clone(); layoutClone.WidgetOrder[0] = "network";
        check("Layout settings clone isolated", layoutDraft.WidgetOrder[0] == "summary");
        layoutClone.WidgetOrder = ["network", "invalid", "network"]; layoutClone.Validate();
        check("Widget order normalized without retired details", layoutClone.WidgetOrder.SequenceEqual(new[] { "network", "summary", "gpus" }));

        check("Different shortcut paths are distinct", ShellDesktopReader.Identity(new DesktopIcon { Path = "C:\\User\\Chrome.lnk" }) != ShellDesktopReader.Identity(new DesktopIcon { Path = "C:\\Public\\Chrome.lnk" }));
        var gate = new EdgeActivationGate();
        check("Edge dwell waits", !gate.Update(true, true, false, 0, 120));
        check("Edge dwell opens once", gate.Update(true, true, false, 120, 120));
        check("Stationary edge never reopens", !gate.Update(true, true, false, 10000, 120));
        check("Release hysteresis prevents jitter", !gate.Update(false, true, false, 11000, 120) && !gate.Update(true, true, false, 12000, 0));
        gate.Update(false, false, false, 13000, 120);
        check("Leaving edge rearms reveal", gate.Update(true, true, false, 14000, 0));
        check("Animation cannot trigger reveal", !gate.Update(true, true, true, 15000, 0));
        check("Auto hide cancels false leave while pointer remains inside",
            AutoHidePolicy.Decide(false, false, false, true) == AutoHideAction.Cancel);
        check("Auto hide restarts full delay after animation",
            AutoHidePolicy.Decide(false, false, true, false) == AutoHideAction.RestartCountdown);
        check("Pinned and suppressed panels cancel pending hide",
            AutoHidePolicy.Decide(true, false, false, false) == AutoHideAction.Cancel
            && AutoHidePolicy.Decide(false, true, false, false) == AutoHideAction.Cancel);
        check("Stable pointer outside permits hide",
            AutoHidePolicy.Decide(false, false, false, false) == AutoHideAction.Hide);
        check("Installed RAM at least OS usable", SystemMetrics.ReadInstalledMemory() >= memory.Total);
    }
}
