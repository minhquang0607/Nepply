using System.Reflection;
using Nepply.Models;

namespace Nepply.Modules.Settings;

[Flags]
internal enum SettingsChange
{
    None = 0, Appearance = 1, Dimensions = 2, Grid = 4, Interaction = 8,
    Desktop = 16, Telemetry = 32, Hotkey = 64, AutoStart = 128,
    DesktopVisibility = 256, Structure = 512
}

/// <summary>One conservative classification shared by preview and commit.
/// Unclassified future settings require a structural refresh, never a silent no-op.</summary>
internal readonly record struct SettingsChangeSet(SettingsChange Changes)
{
    private static readonly PropertyInfo[] Properties = typeof(AppSettings).GetProperties()
        .Where(p => p.CanRead && p.CanWrite).ToArray();
    internal bool Has(SettingsChange group) => (Changes & group) != 0;
    internal bool CanPreviewWithoutRebuild => (Changes & ~(SettingsChange.Appearance |
        SettingsChange.Dimensions | SettingsChange.Grid | SettingsChange.Interaction)) == 0;
    internal bool OnlyDimensions => (Changes & ~SettingsChange.Dimensions) == 0;

    internal static SettingsChangeSet Between(AppSettings before, AppSettings after)
    {
        SettingsChange changes = SettingsChange.None;
        foreach (var property in Properties)
        {
            var a = property.GetValue(before);
            var b = property.GetValue(after);
            bool equal = a is string[] left && b is string[] right
                ? left.SequenceEqual(right) : Equals(a, b);
            if (!equal) changes |= Classify(property.Name);
        }
        return new(changes);
    }

    private static SettingsChange Classify(string name) => name switch
    {
        nameof(AppSettings.Theme) or nameof(AppSettings.Accent) or nameof(AppSettings.CustomAccent)
            or nameof(AppSettings.PanelOpacity) or nameof(AppSettings.GlowStrength)
            or nameof(AppSettings.DynamicMaterial) or nameof(AppSettings.MetallicEnabled)
            or nameof(AppSettings.Motion) or nameof(AppSettings.NeonEnabled) or nameof(AppSettings.Language)
            => SettingsChange.Appearance,
        nameof(AppSettings.PanelWidth) or nameof(AppSettings.TopPanelWidth) or nameof(AppSettings.PanelHeight)
            or nameof(AppSettings.PanelWidthPercent) or nameof(AppSettings.TopPanelWidthPercent)
            or nameof(AppSettings.PanelHeightPercent) => SettingsChange.Dimensions,
        nameof(AppSettings.GridColumns) or nameof(AppSettings.GridRows)
            or nameof(AppSettings.TopGridColumns) or nameof(AppSettings.TopGridRows) => SettingsChange.Grid,
        nameof(AppSettings.HideDelayMs) or nameof(AppSettings.TriggerDelayMs)
            or nameof(AppSettings.TriggerZoneSize) => SettingsChange.Interaction,
        nameof(AppSettings.ShowHiddenFiles) or nameof(AppSettings.ShowSystemIcons)
            or nameof(AppSettings.MergeEquivalentShortcuts) => SettingsChange.Desktop,
        nameof(AppSettings.ShowMonitor) or nameof(AppSettings.HardwareSensorsEnabled)
            or nameof(AppSettings.MonitorUpdateIntervalMs) or nameof(AppSettings.NetworkInterfaceId)
            or nameof(AppSettings.EcoMode) or nameof(AppSettings.SuspendMonitorWhenHidden)
            or nameof(AppSettings.GpuDisplay) or nameof(AppSettings.GpuIdentifier)
            or nameof(AppSettings.IntegratedGpuIdentifier) => SettingsChange.Telemetry,
        nameof(AppSettings.HotkeyKey) or nameof(AppSettings.HotkeyModifiers) => SettingsChange.Hotkey,
        nameof(AppSettings.AutoStart) => SettingsChange.AutoStart,
        nameof(AppSettings.HideDesktopIcons) => SettingsChange.DesktopVisibility,
        _ => SettingsChange.Structure
    };
}
