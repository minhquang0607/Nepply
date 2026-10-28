using Nepply.Models;

namespace Nepply.Services;

public sealed record LayoutPreset(string Id, string NameKey, string DescriptionKey, double PreviewWidth, double PreviewHeight)
{
    public string Name => LocalizationService.Get(NameKey);
    public string Description => LocalizationService.Get(DescriptionKey);
}

internal static class LayoutPresets
{
    internal static IReadOnlyList<LayoutPreset> All { get; } = Array.AsReadOnly(new[]
    {
        new LayoutPreset("compact", "CompactPreset", "CompactPresetDescription", 28, 44),
        new LayoutPreset("spacious", "SpaciousPreset", "SpaciousPresetDescription", 42, 44),
        new LayoutPreset("ribbon", "RibbonPreset", "RibbonPresetDescription", 88, 22)
    });

    internal static AppSettings Apply(string id, AppSettings source)
    {
        // A geometry preset must preserve the user's palette, effects and private settings.
        var profile = LayoutProfile.Capture(source) with { WidthPercent = 0, TopWidthPercent = 0, TopHeightPercent = 0 };
        var side = source.EdgePosition == EdgePosition.Right ? EdgePosition.Right : EdgePosition.Left;
        profile = id switch
        {
            "compact" => profile with
            {
                Edge = side,
                Width = 340,
                MonitorHeight = 340,
                SummaryColumns = 2,
                GpuColumns = 1
            },
            "spacious" => profile with
            {
                Edge = side,
                Width = 460,
                MonitorHeight = 400,
                SummaryColumns = 2,
                GpuColumns = 2
            },
            "ribbon" => profile with
            {
                Edge = source.EdgePosition.IsHorizontal() ? source.EdgePosition : EdgePosition.Top,
                TopWidth = 1520,
                TopHeight = 280,
                MonitorWidth = 420,
                SummaryColumns = 2,
                GpuColumns = 2
            },
            _ => throw new ArgumentOutOfRangeException(nameof(id), "Unknown layout preset.")
        };
        return profile.ApplyTo(source);
    }
}
