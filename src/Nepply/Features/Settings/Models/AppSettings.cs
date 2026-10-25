using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Nepply.Models;

public enum EdgePosition { Left, Right, Top, Bottom }

public static class EdgePositionExtensions
{
    public static bool IsHorizontal(this EdgePosition edge) => edge is EdgePosition.Top or EdgePosition.Bottom;
}
public enum IconSizeOption { Small, Medium, Large }
public enum ThemeOption { Dark, Light, System }
public enum MotionMode { Smooth, Quick, Off }
public enum GpuDisplayMode { All, Primary, Secondary }

public sealed class AppSettings
{
    public int SchemaVersion { get; set; }
    public bool HasCompletedOnboarding { get; set; }
    public EdgePosition EdgePosition { get; set; } = EdgePosition.Left;
    public int PanelWidth { get; set; } = 380;
    public int PanelHeight { get; set; } = 320;
    public int TopPanelWidth { get; set; } = 1440;
    // Zero preserves legacy DIP sizing until Settings writes a relative preference.
    public double PanelWidthPercent { get; set; }
    public double TopPanelWidthPercent { get; set; }
    public double PanelHeightPercent { get; set; }
    public int LayoutVersion { get; set; }
    public int GridColumns { get; set; } = 4;
    public int GridRows { get; set; } = 5;
    public int TopGridColumns { get; set; } = 6;
    public int TopGridRows { get; set; } = 1;
    [JsonIgnore] public int ActiveGridColumns => EdgePosition.IsHorizontal() ? TopGridColumns : GridColumns;
    [JsonIgnore] public int ActiveGridRows => EdgePosition.IsHorizontal() ? TopGridRows : GridRows;
    public double GridCellWidth { get; set; } = 88;
    public double GridCellHeight { get; set; } = 90;
    public double PanelOpacity { get; set; } = 0.98;
    public bool AutoStart { get; set; }
    public bool HideDesktopIcons { get; set; }
    public int HideDelayMs { get; set; } = 600;
    public int MonitorUpdateIntervalMs { get; set; } = 1500;
    public IconSizeOption IconSize { get; set; } = IconSizeOption.Medium;
    public bool ShowMonitor { get; set; } = true;
    public int TriggerZoneSize { get; set; } = 4;
    public int TriggerDelayMs { get; set; } = 120;
    public bool SingleClickToOpen { get; set; } = true;
    public bool EcoMode { get; set; } = true;
    public bool SuspendMonitorWhenHidden { get; set; } = true;
    public bool MetallicEnabled { get; set; } = true;
    public bool NeonEnabled { get; set; } = true;
    public string Language { get; set; } = "vi";
    public MotionMode Motion { get; set; } = MotionMode.Smooth;
    public bool DynamicMaterial { get; set; } = true;
    public double GlowStrength { get; set; } = .22;
    public int MonitorAreaHeight { get; set; } = 380;
    public int MonitorAreaWidth { get; set; } = 370;
    public bool IconsFirst { get; set; }
    public bool ShowNetwork { get; set; } = true;
    public string[] WidgetOrder { get; set; } = ["summary", "gpus", "network"];
    public string[] IconOrder { get; set; } = [];
    public int SummaryColumns { get; set; }
    public int GpuColumns { get; set; }
    public int IconTileShape { get; set; }
    public bool HardwareSensorsEnabled { get; set; } = true;
    public bool ShowHiddenFiles { get; set; }
    public bool ShowSystemIcons { get; set; } = true;
    public bool MergeEquivalentShortcuts { get; set; } = true;
    public ThemeOption Theme { get; set; } = ThemeOption.Dark;
    public string Accent { get; set; } = "Champagne";
    public string CustomAccent { get; set; } = "#D9BC8B";
    public GpuDisplayMode GpuDisplay { get; set; } = GpuDisplayMode.All;
    public string IntegratedGpuIdentifier { get; set; } = "";
    public string MonitorDevice { get; set; } = "";
    public string NetworkInterfaceId { get; set; } = "";
    public string GpuIdentifier { get; set; } = "";
    public int HotkeyModifiers { get; set; } = 2;
    public int HotkeyKey { get; set; } = 32;
    // Rebound shortcuts other than TogglePanel; see ShortcutBindings for the format.
    public string[] Shortcuts { get; set; } = [];
    [JsonIgnore] public int IconSizePx => PixelsFor(IconSize);
    public static int PixelsFor(IconSizeOption size) => size switch { IconSizeOption.Small => 28, IconSizeOption.Large => 44, _ => 36 };
    [JsonIgnore] public static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nepply", "settings.json");

    public void Validate()
    {
        if (!Enum.IsDefined(EdgePosition)) EdgePosition = EdgePosition.Left;
        if (!Enum.IsDefined(IconSize)) IconSize = IconSizeOption.Medium;
        if (!Enum.IsDefined(Theme)) Theme = ThemeOption.Dark;
        PanelWidth = Math.Clamp(PanelWidth, 320, 600);
        if(!Nepply.Services.LocalizationService.IsSupported(Language)) Language="vi";
        PanelWidthPercent = ValidPercent(PanelWidthPercent);
        TopPanelWidthPercent = ValidPercent(TopPanelWidthPercent);
        PanelHeightPercent = ValidPercent(PanelHeightPercent);
        MonitorAreaHeight = Math.Clamp(MonitorAreaHeight, 180, 560);
        MonitorAreaWidth = Math.Clamp(MonitorAreaWidth, 240, 600);
        SummaryColumns = Math.Clamp(SummaryColumns, 0, 2);
        GpuColumns = Math.Clamp(GpuColumns, 0, 2);
        IconTileShape = Math.Clamp(IconTileShape, 0, 2);
        GlowStrength = double.IsFinite(GlowStrength) ? Math.Clamp(GlowStrength, 0, .5) : .22;
        if(!Enum.IsDefined(Motion)) Motion=MotionMode.Smooth;
        WidgetOrder = (WidgetOrder ?? []).Where(x => x is "summary" or "gpus" or "network").Concat(new[] { "summary", "gpus", "network" }).Distinct().ToArray();
        IconOrder = (IconOrder ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Take(10000).ToArray();
        if (LayoutVersion < 1) { if (PanelHeight == 520) PanelHeight = 320; LayoutVersion = 1; }
        PanelHeight = Math.Clamp(PanelHeight, 240, 600);
        TopPanelWidth = Math.Clamp(TopPanelWidth, 720, 3200);
        GridColumns = Math.Clamp(GridColumns, 1, 5);
        GridRows = Math.Clamp(GridRows, 1, Nepply.Modules.Desktop.GridCapacity.MaximumRows);
        TopGridColumns = Math.Clamp(TopGridColumns, 1, Nepply.Modules.Desktop.GridCapacity.MaximumColumns);
        TopGridRows = Math.Clamp(TopGridRows, 1, 3);
        GridCellWidth = double.IsFinite(GridCellWidth) ? Math.Clamp(GridCellWidth, 72, 160) : 88;
        GridCellHeight = double.IsFinite(GridCellHeight) ? Math.Clamp(GridCellHeight, 82, 160) : 90;
        PanelOpacity = double.IsFinite(PanelOpacity) ? Math.Clamp(PanelOpacity, 0.8, 1) : 0.98;
        HideDelayMs = Math.Clamp(HideDelayMs, 200, 3000);
        MonitorUpdateIntervalMs = Math.Clamp(MonitorUpdateIntervalMs, 1000, 10000);
        TriggerZoneSize = Math.Clamp(TriggerZoneSize, 2, 16);
        TriggerDelayMs = Math.Clamp(TriggerDelayMs, 0, 1000);
        if (!Services.ThemeService.Palettes.ContainsKey(Accent ?? "")) Accent = "Champagne";
        if (!IsHexColor(CustomAccent)) CustomAccent = "#D9BC8B";
        if (!Enum.IsDefined(GpuDisplay)) GpuDisplay = GpuDisplayMode.All;
        // Device identifiers come from Windows/LibreHardwareMonitor and are short. An
        // oversized value (e.g. a hand-edited file) falls back to automatic selection
        // instead of being rejected by the sensor helper on every read.
        IntegratedGpuIdentifier = ValidIdentifier(IntegratedGpuIdentifier);
        MonitorDevice = ValidIdentifier(MonitorDevice); NetworkInterfaceId = ValidIdentifier(NetworkInterfaceId);
        GpuIdentifier = ValidIdentifier(GpuIdentifier);
        // Same rule as the hotkey recorder: Ctrl or Alt is required, so a bare
        // Shift+key can never be registered globally and swallow normal typing.
        if (!ShortcutBindings.IsValid(ShortcutAction.TogglePanel, new(HotkeyModifiers, HotkeyKey)))
        { HotkeyModifiers = ModControl; HotkeyKey = 32; }
        Shortcuts = ShortcutBindings.Normalize(this);
    }
    public static bool IsHexColor(string? color) => color is { Length: 7 } && color[0] == '#' && color.AsSpan(1).ToArray().All(Uri.IsHexDigit);
    public AppSettings Clone() { var copy = (AppSettings)MemberwiseClone(); copy.WidgetOrder = (string[])WidgetOrder.Clone(); copy.IconOrder = (string[])IconOrder.Clone(); copy.Shortcuts = (string[])Shortcuts.Clone(); return copy; }
    public void CopyFrom(AppSettings source)
    {
        foreach (var property in typeof(AppSettings).GetProperties().Where(p => p.CanWrite))
            property.SetValue(this, property.GetValue(source));
        Validate();
    }
    internal const int MaximumIdentifierLength = 2048;
    private const int ModControl = 2;
    private static string ValidIdentifier(string? value) => value is { Length: <= MaximumIdentifierLength } ? value : "";
    private static double ValidPercent(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;
    public void ResetToDefaults() => CopyFrom(new AppSettings { HasCompletedOnboarding = HasCompletedOnboarding });
    private static Services.SettingsStore Store = new(SettingsPath,
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductIdentity.LegacyName, "settings.json"));
    internal static void UseTestSettingsStore(string path) => Store = new(path);
    [JsonIgnore] public static UserMessage? RecoveryNotice => Store.RecoveryNotice;
    public void Save() => Store.Save(this);
    public static AppSettings Load() => Store.Load();
}
