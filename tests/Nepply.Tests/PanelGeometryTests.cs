using Nepply.Models;
using Nepply.Modules.Panel;

internal static class PanelGeometryTests
{
    internal static void Run(Action<string, bool> check)
    {
        check("Bottom append preserves serialized legacy edges", (int)EdgePosition.Left == 0 && (int)EdgePosition.Right == 1 && (int)EdgePosition.Top == 2 && (int)EdgePosition.Bottom == 3);
        var bottomSettings = new AppSettings { EdgePosition = EdgePosition.Bottom, TopGridRows = 3, TopGridColumns = 8 };
        var bottomRestore = Nepply.Services.LayoutProfile.Parse(Nepply.Services.LayoutProfile.Capture(bottomSettings).ToJson()).ApplyTo(new());
        check("Bottom profile roundtrip retains edge and horizontal grid", bottomRestore.EdgePosition == EdgePosition.Bottom && bottomRestore.ActiveGridRows == 3 && bottomRestore.ActiveGridColumns == 8);
        check("Ribbon preset retains bottom anchor", Nepply.Services.LayoutPresets.Apply("ribbon", bottomSettings).EdgePosition == EdgePosition.Bottom);
        check("Bottom hit test accepts last work-area pixel", EdgeHitTest.Contains(EdgePosition.Bottom, -100, 879, -1920, -200, 0, 880, 4));
        check("Bottom hit test rejects taskbar and other display", !EdgeHitTest.Contains(EdgePosition.Bottom, -100, 880, -1920, -200, 0, 880, 4) && !EdgeHitTest.Contains(EdgePosition.Bottom, 1, 879, -1920, -200, 0, 880, 4));
        check("Edge distance counts from each trigger edge",
            EdgeHitTest.DistanceToEdge(EdgePosition.Left, -1910, 0, -1920, -200, 0, 880) == 10
            && EdgeHitTest.DistanceToEdge(EdgePosition.Right, -1, 0, -1920, -200, 0, 880) == 0
            && EdgeHitTest.DistanceToEdge(EdgePosition.Top, -100, -195, -1920, -200, 0, 880) == 5
            && EdgeHitTest.DistanceToEdge(EdgePosition.Bottom, -100, 879, -1920, -200, 0, 880) == 0);
        check("Edge distance is unknown on another display", EdgeHitTest.DistanceToEdge(EdgePosition.Bottom, 1, 879, -1920, -200, 0, 880) == null);
        check("Edge polling stays responsive near the edge", EdgePolling.Interval(false, 100, 164) == EdgePolling.Responsive);
        check("Edge polling stays responsive while the panel is active", EdgePolling.Interval(true, null, 164) == EdgePolling.Responsive);
        check("Edge polling idles far from the edge", EdgePolling.Interval(false, 165, 164) == EdgePolling.Idle
            && EdgePolling.Interval(false, null, 164) == EdgePolling.Idle && EdgePolling.Idle > EdgePolling.Responsive);
        check("Bottom hit test uses exact zone depth", EdgeHitTest.Contains(EdgePosition.Bottom, -100, 876, -1920, -200, 0, 880, 4) && !EdgeHitTest.Contains(EdgePosition.Bottom, -100, 875, -1920, -200, 0, 880, 4));
        foreach (int pixels in new[] { 1280, 1920, 2560, 3840 })
            foreach (double dpi in new[] { 1d, 1.25, 1.5, 2d })
            {
                var screen = new Nepply.Helpers.DisplayScreen("fixture", 0, 0, pixels, 2160, dpi, true);
                var settings = new AppSettings { PanelWidthPercent = 100, TopPanelWidthPercent = 100, PanelHeightPercent = 100 };
                var available = PanelDimensions.Available(screen);
                var full = PanelDimensions.Resolve(settings, screen);
                check($"Relative 100% fills available work area {pixels}/{dpi}", full.Side == available.Width && full.Top == available.Width && full.Height == available.Height);
                settings.TopPanelWidthPercent = 70;
                var partial = PanelDimensions.Resolve(settings, screen);
                check($"Relative dimension grows up to the slider endpoint {pixels}/{dpi}", partial.Top < full.Top || available.Width <= 720);
            }
        var original = new AppSettings(); var resized = original.Clone(); resized.PanelWidthPercent = 60;
        check("Dimension-only preview bypasses structural refresh", PanelDimensions.OnlyDimensionsChanged(original, resized));
        resized.IconOrder = ["changed"];
        check("Structural changes cannot enter dimension-only preview", !PanelDimensions.OnlyDimensionsChanged(original, resized));
        var profile = Nepply.Services.LayoutProfile.Capture(new AppSettings { TopPanelWidthPercent = 82, PanelHeightPercent = 40 });
        var restored = Nepply.Services.LayoutProfile.Parse(profile.ToJson()).ApplyTo(original);
        check("Relative geometry survives profile export/import", restored.TopPanelWidthPercent == 82 && restored.PanelHeightPercent == 40);
        check("Preset resets relative overrides", Nepply.Services.LayoutPresets.Apply("ribbon", restored).TopPanelWidthPercent == 0);
        foreach (double scale in new[] { 1d, 1.25, 1.5, 1.75, 2, 2.25 })
            foreach (var edge in Enum.GetValues<EdgePosition>())
            {
                var p = PanelGeometry.Calculate(-1920, -200, 1920, 1080, scale, edge, 380, 1440, 320, false);
                check($"Panel geometry {edge}/{scale} remains inside work area",
                    p.X >= -1920 && p.Y >= -200 && p.X + p.PixelWidth <= 0 && p.Y + p.PixelHeight <= 880);
                check($"Panel geometry {edge}/{scale} DIP and physical bounds agree",
                    Math.Abs(p.Width * scale - p.PixelWidth) < .00001 && Math.Abs(p.Height * scale - p.PixelHeight) < .00001);
                check($"Panel geometry {edge}/{scale} has intended anchor",
                    edge.IsHorizontal() ? Math.Abs((p.X + p.PixelWidth / 2d) - -960) <= .5
                    : edge == EdgePosition.Left ? p.X == -1920 + (int)Math.Round(12 * scale)
                    : p.X + p.PixelWidth == -(int)Math.Round(12 * scale));
                if (edge == EdgePosition.Bottom)
                    check($"Bottom anchor clears work-area boundary {scale}", p.Y + p.PixelHeight == 880 - (int)Math.Round(12 * scale));
            }
        var tiny = PanelGeometry.Calculate(100, 200, 1, 1, 2, EdgePosition.Right, 380, 1440, 320, false);
        check("Tiny work area never places panel beyond its only pixel", tiny.X == 100 && tiny.Y == 200 && tiny.PixelWidth == 1 && tiny.PixelHeight == 1);
        var edit = PanelGeometry.Calculate(0, 0, 1920, 1080, 1, EdgePosition.Top, 380, 1440, 240, true);
        check("Top editing retains minimum usable height", edit.Height == 400);
        check("Hidden monitor reserves no horizontal space", PanelGeometry.MonitorWidth(false, 370, 1000) == 0);
        check("Monitor width respects actual available space", PanelGeometry.MonitorWidth(true, 370, 500) == 200);
        check("Monitor width respects preferred size", PanelGeometry.MonitorWidth(true, 240, 1000) == 240);
        check("Transient invalid width does not poison WPF layout", PanelGeometry.MonitorWidth(true, 370, double.NaN) == 0);
        bool rejected = false;
        try { PanelGeometry.Calculate(0, 0, 1920, 1080, 0, EdgePosition.Left, 380, 1440, 320, false); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check("Invalid screen scale is rejected before native placement", rejected);
    }
}
