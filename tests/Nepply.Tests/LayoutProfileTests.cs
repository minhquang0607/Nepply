using Nepply.Models;
using Nepply.Services;

internal static class LayoutProfileTests
{
    public static void Run(Action<string, bool> check)
    {
        var source = new AppSettings { EdgePosition = EdgePosition.Top, GridCellWidth = 114, GridCellHeight = 123, Accent = "Custom", CustomAccent = "#33CCFF", IconOrder = ["C:\\private\\item.lnk"], GpuIdentifier = "private-device", AutoStart = true, HideDesktopIcons = true };
        var json = LayoutProfile.Capture(source).ToJson();
        var flat = source.Clone(); flat.MetallicEnabled = false;
        var sized = source.Clone(); sized.IconSize = IconSizeOption.Small;
        check("Layout roundtrip preserves icon size", LayoutProfile.Parse(LayoutProfile.Capture(sized).ToJson()).ApplyTo(source).IconSize == IconSizeOption.Small);
        check("Icon sizes map to distinct pixel sizes", AppSettings.PixelsFor(IconSizeOption.Small) == 28 && AppSettings.PixelsFor(IconSizeOption.Medium) == 36
            && AppSettings.PixelsFor(IconSizeOption.Large) == 44 && new AppSettings { IconSize = IconSizeOption.Large }.IconSizePx == 44);
        check("Layout roundtrip preserves material disabled", !LayoutProfile.Parse(LayoutProfile.Capture(flat).ToJson()).ApplyTo(source).MetallicEnabled);
        check("Legacy layout defaults material enabled", LayoutProfile.Parse("{\"Version\":1}").ApplyTo(source).MetallicEnabled);
        check("Layout export excludes private paths and device identifiers", !json.Contains("private") && !json.Contains("IconOrder") && !json.Contains("GpuIdentifier"));
        check("Layout export excludes system behavior settings", !json.Contains("AutoStart") && !json.Contains("HideDesktopIcons"));
        var target = new AppSettings { HasCompletedOnboarding = true, GpuIdentifier = "local-gpu", MonitorDevice = "local-screen", IconOrder = ["local-icon"] };
        var imported = LayoutProfile.Parse(json).ApplyTo(target);
        check("Layout roundtrip retains geometry and custom accent", imported.EdgePosition == EdgePosition.Top && imported.GridCellWidth == 114 && imported.GridCellHeight == 123 && imported.CustomAccent == "#33CCFF");
        check("Layout import preserves local permissions and identifiers", !imported.AutoStart && !imported.HideDesktopIcons && imported.HasCompletedOnboarding && imported.GpuIdentifier == "local-gpu" && imported.MonitorDevice == "local-screen" && imported.IconOrder.SequenceEqual(target.IconOrder));
        check("Layout import does not mutate live settings", target.EdgePosition == EdgePosition.Left && target.GridCellWidth == 88);
        var privateSettings = new AppSettings
        {
            EdgePosition = EdgePosition.Right,
            Accent = "Custom",
            CustomAccent = "#11CCEE",
            DynamicMaterial = false,
            GlowStrength = .37,
            PanelOpacity = .91,
            MonitorDevice = "private-screen",
            NetworkInterfaceId = "private-network",
            GpuIdentifier = "private-gpu",
            IntegratedGpuIdentifier = "private-igpu",
            HideDesktopIcons = true,
            AutoStart = true,
            IconOrder = ["private-icon"],
            HardwareSensorsEnabled = false
        };
        var compact = LayoutPresets.Apply("compact", privateSettings);
        check("Compact preset keeps current side", compact.EdgePosition == EdgePosition.Right && compact.PanelWidth == 340);
        check("Preset preserves palette and effects", compact.Accent == "Custom" && compact.CustomAccent == "#11CCEE"
            && !compact.DynamicMaterial && compact.GlowStrength == .37 && compact.PanelOpacity == .91);
        check("Preset preserves devices and system behavior", compact.MonitorDevice == "private-screen"
            && compact.NetworkInterfaceId == "private-network" && compact.GpuIdentifier == "private-gpu"
            && compact.IntegratedGpuIdentifier == "private-igpu"
            && compact.HideDesktopIcons && compact.AutoStart && !compact.HardwareSensorsEnabled
            && compact.IconOrder.SequenceEqual(["private-icon"]));
        var ribbon = LayoutPresets.Apply("ribbon", privateSettings);
        check("Ribbon preset is wide and shallow", ribbon.EdgePosition == EdgePosition.Top
            && ribbon.TopPanelWidth == 1520 && ribbon.PanelHeight == 280 && ribbon.GridColumns == privateSettings.GridColumns && ribbon.GridRows == privateSettings.GridRows);
        var spacious = LayoutPresets.Apply("spacious", privateSettings);
        check("Spacious preset enlarges side layout", spacious.EdgePosition == EdgePosition.Right
            && spacious.PanelWidth == 460 && spacious.GridColumns == privateSettings.GridColumns && spacious.GpuColumns == 2);
        bool unknownPresetRejected = false;
        try { LayoutPresets.Apply("unknown", privateSettings); } catch (ArgumentOutOfRangeException) { unknownPresetRejected = true; }
        check("Unknown preset rejected without fallback", unknownPresetRejected);
        var hostile = LayoutProfile.Parse("{\"Version\":1,\"AutoStart\":true,\"HideDesktopIcons\":true,\"Width\":-100,\"WidgetOrder\":null}").ApplyTo(target);
        check("Layout ignores injected behavior and normalizes bounds", !hostile.AutoStart && !hostile.HideDesktopIcons && hostile.PanelWidth == 320 && hostile.WidgetOrder.SequenceEqual(["summary", "gpus", "network"]));
        foreach (var invalid in new[] { "{}", "{\"Version\":99}", "[]", new string(' ', 65537) })
        {
            bool rejected = false;
            try { LayoutProfile.Parse(invalid); } catch (Exception ex) when (ex is NotSupportedException or System.IO.InvalidDataException) { rejected = true; }
            check("Invalid or oversized layout rejected: " + invalid.Length, rejected);
        }
    }
}
