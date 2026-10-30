using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nepply.Models;
using Nepply.Services;
using Nepply.ViewModels;
using Nepply.Views;
using Nepply.Services.Telemetry;

internal static class MonitorMaterialTests
{
    internal static void Run(Action<string, bool> check, Action<int> pump, string output, Action<FrameworkElement, int, int, string> render)
    {
        var monitor = new MonitorPanel { Width = 380 };
        var host = new Window { Content = monitor, SizeToContent = SizeToContent.WidthAndHeight, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
        var settings = new AppSettings { Accent = "Gold", DynamicMaterial = true };
        var vm = (HardwareMonitorViewModel)monitor.DataContext;
        try
        {
            ThemeService.ApplyAccessibility(settings, false, true);
            monitor.ConfigureLayout(settings, false);
            monitor.Initialize(new FixtureMonitor()); monitor.SetActive(true);
            vm.Gpus.Add(new(new GpuReading("igpu", "Integrated graphics", 12, 44, 400, "fixture") { Label = "iGPU" }));
            host.Show(); pump(60); host.UpdateLayout();
            var owner = (FrameworkElement)monitor.FindName("Widgets");
            var light = (MaterialLightScope)owner.GetValue(MetallicSurface.LightScopeProperty);
            var firstClock = light.Clock;
            check("Monitor owns one active shared light", firstClock != null);
            var originalWidgets = ((Panel)owner).Children.Cast<UIElement>().ToArray();
            settings.GridColumns = 3; monitor.ConfigureLayout(settings, false); pump(30);
            check("Grid-only configuration retains monitor visuals and clock", originalWidgets.SequenceEqual(((Panel)owner).Children.Cast<UIElement>()) && ReferenceEquals(firstClock, light.Clock));
            foreach (var edge in new[] { EdgePosition.Left, EdgePosition.Top, EdgePosition.Bottom })
                foreach (var count in new[] { 1, 2, 3 })
                {
                    monitor.Height = double.NaN;
                    settings.EdgePosition = edge; monitor.ConfigureLayout(settings, false);
                    while (vm.Gpus.Count < count) vm.Gpus.Add(new(new GpuReading("gpu" + vm.Gpus.Count, "Dedicated graphics", 48, 61, 1800, "fixture")));
                    while (vm.Gpus.Count > count) vm.Gpus.RemoveAt(vm.Gpus.Count - 1);
                    pump(30); host.UpdateLayout();
                    var settling = System.Diagnostics.Stopwatch.StartNew();
                    while (Children(monitor).OfType<ProgressBar>().Any(p => p.HasAnimatedProperties) && settling.ElapsedMilliseconds < 2000) pump(20);
                    var cards = Children(monitor).OfType<Border>().Where(b => MetallicSurface.GetAccent(b) != null).ToArray();
                    var readings = Children(monitor).OfType<MetricReadingView>().ToArray();
                    var network = (Border)monitor.FindName("NetworkCard");
                    check($"Network card compact without losing rates {edge}/{count}", network.ActualHeight <= 62 && Children(network).OfType<TextBlock>().Count(t => t.Text.Contains("KiB/s")) == 2);
                    check($"Shared metric presentation {edge}/{count} GPU", readings.Length == 2 + count && readings.Any(r => r.Label == "CPU") && readings.Any(r => r.Label == "RAM" && r.SecondaryText == ""));
                    check($"Equal metric dimensions {edge}/{count} GPU", cards.Length == 2 + count && cards.All(c => Math.Abs(c.ActualWidth - cards[0].ActualWidth) < .1 && Math.Abs(c.ActualHeight - cards[0].ActualHeight) < .1));
                    check($"Common light coordinates {edge}/{count} GPU", cards.All(c =>
                    {
                        if (c.Background is not LinearGradientBrush b || b.MappingMode != BrushMappingMode.Absolute) return false;
                        var origin = c.TranslatePoint(new Point(), owner);
                        return Math.Abs(origin.X + b.StartPoint.X) < .1 && Math.Abs(origin.Y + b.StartPoint.Y) < .1
                            && Math.Abs((b.EndPoint - b.StartPoint).X - MaterialLightScope.Period) < .1;
                    }));
                    var phases = cards.Select(c => ((LinearGradientBrush)c.Background).Transform.Value.OffsetX).ToArray();
                    check($"All cards share light phase {edge}/{count} GPU", phases.Max() - phases.Min() < .000001);
                    render(monitor, 380, (int)Math.Ceiling(monitor.ActualHeight), Path.Combine(output, $"equal-cards-{edge}-{count}.png"));
                }
            var pointer = light.Pointer;
            void SettlePointer()
            {
                // Direct offsets need no settling, but WPF clock commands need a timing tick.
                pump(60);
            }
            pointer?.MoveTo(new Point(100, 100));
            pointer?.MoveTo(new Point(100, 80)); SettlePointer();
            var pointerCards = Children(monitor).OfType<Border>().Where(b => MetallicSurface.GetAccent(b) != null).ToArray();
            check($"Upward motion moves light diagonally upward ({pointer?.Transform.X:F3}, {pointer?.Transform.Y:F3}; {owner.ActualWidth:F0}x{owner.ActualHeight:F0})", pointer is { } && Math.Abs(pointer.Transform.X + 7) < .01 && Math.Abs(pointer.Transform.Y + 7) < .01
                && pointerCards.Select(c => ((LinearGradientBrush)c.Background).Transform).Distinct().Count() == 1);
            check("Mouse interaction pauses existing sweep", ReferenceEquals(firstClock, light.Clock) && light.Clock?.IsPaused == true);
            pump(60); // ClockController commands settle on the next WPF timing tick.
            var heldTime = light.Clock?.CurrentTime; pump(350);
            // DesiredFrameRate quantizes WPF clock timestamps, including paused clocks, and on a
            // slow runner the pause can land one tick after the sample (CI saw 22.9 ms). Allow two
            // frames of rounding; a running sweep would advance about 350 ms during the wait.
            check($"Sweep stays paused within two timing ticks ({heldTime} -> {light.Clock?.CurrentTime})",
                light.Clock?.IsPaused == true && heldTime is { } held && light.Clock.CurrentTime is { } current
                && Math.Abs((current - held).TotalMilliseconds) <= 2 * 1000d / 60);
            pointer?.MoveTo(null); SettlePointer();
            check("Mouse leave resumes existing sweep", ReferenceEquals(firstClock, light.Clock) && light.Clock?.IsPaused == false);
            check($"Mouse leave preserves light offset ({pointer?.Transform.X:F3}, {pointer?.Transform.Y:F3})", pointer is { } && Math.Abs(pointer.Transform.X + 7) < .01 && Math.Abs(pointer.Transform.Y + 7) < .01);
            check("Settled pointer leaves no running animation clocks", pointer is { } && !pointer.Transform.HasAnimatedProperties);
            check("Usage meters stay prominent in every card", Children(monitor).OfType<ProgressBar>().All(p => p.ActualHeight == 8));
            check("Monitor cards contain no decorative icon glyphs", !Children(monitor).OfType<TextBlock>().Any(t => t.Text.Any(c => c >= '\uE000' && c <= '\uF8FF')));
            var clockBefore = light.Clock;
            vm.Gpus.Add(new(new GpuReading("late", "Late device", 1, 40, 500, "fixture"))); pump(30);
            check("Late GPU joins existing light without restart", ReferenceEquals(clockBefore, light.Clock));
            host.Hide(); pump(30);
            check("Hidden monitor releases shared animation and mouse input", light.Clock == null && light.Pointer == null && pointer is { } && !pointer.Transform.HasAnimatedProperties);
            host.Show(); pump(30);
            check("Shown monitor resumes one shared light", light.Clock != null);
            settings.Motion = MotionMode.Off; ThemeService.ApplyAccessibility(settings, false, true); pump(30);
            check("Motion off stops shared monitor light and pointer input", light.Clock == null && light.Pointer == null);
            settings.NeonEnabled = false; ThemeService.ApplyAccessibility(settings, false, true); pump(30);
            check("Neon toggle removes all metric glow", Children(monitor).OfType<Border>().Where(b => MetallicSurface.GetAccent(b) != null).All(b => b.Effect == null));
            check("Neon toggle survives layout profile", !LayoutProfile.Capture(settings).ApplyTo(new()).NeonEnabled);
            monitor.Cleanup();
            check("Monitor cleanup releases shared light", light.Clock == null);
        }
        finally { monitor.Cleanup(); host.Close(); ThemeService.Apply(new()); }
    }
    private sealed class FixtureMonitor : IHardwareMonitor
    {
        public event Action<HardwareInfoSnapshot>? HardwareUpdated { add { } remove { } }
        public HardwareInfoSnapshot GetSnapshot()
        {
            var gpu = new GpuReading("igpu", "AMD Radeon(TM) Graphics", 16, 40, 1900, "fixture") { Kind = GpuKind.Integrated, Label = "iGPU" };
            return new(1, DateTimeOffset.UtcNow, 18, 10.26, 13.9, 74, 18000, 51000, "Wi-Fi",
                HardwareReading.Empty with { CpuTemp = 42, CpuClockMHz = 2810, Gpus = [gpu], VisibleGpus = [gpu], MonotonicTimestamp = System.Diagnostics.Stopwatch.GetTimestamp() }, []);
        }
        public void Configure(AppSettings settings, bool visible) { }
        public void Start() { }
        public void Dispose() { }
    }
    private static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Children(child)) yield return nested;
        }
    }
}
