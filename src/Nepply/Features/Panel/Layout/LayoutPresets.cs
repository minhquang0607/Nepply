using Nepply.Models;

namespace Nepply.Services;

public sealed record LayoutPreset(string Id, string Name, string Description, double PreviewWidth, double PreviewHeight);

internal static class LayoutPresets
{
    internal static IReadOnlyList<LayoutPreset> All { get; } = Array.AsReadOnly(new[]
    {
        new LayoutPreset("compact", "Gọn", "Cạnh bên · gọn", 28, 44),
        new LayoutPreset("spacious", "Thoáng", "Cạnh bên · thoáng", 42, 44),
        new LayoutPreset("ribbon", "Dải ngang", "Cạnh trên / dưới · thấp rộng", 88, 22)
    });

    internal static AppSettings Apply(string id, AppSettings source)
    {
        // A geometry preset must preserve the user's palette, effects and private settings.
        var profile = LayoutProfile.Capture(source) with { WidthPercent=0, TopWidthPercent=0, TopHeightPercent=0 };
        var side = source.EdgePosition == EdgePosition.Right ? EdgePosition.Right : EdgePosition.Left;
        profile = id switch
        {
            "compact" => profile with { Edge=side, Width=340,
                MonitorHeight=340, SummaryColumns=2, GpuColumns=1 },
            "spacious" => profile with { Edge=side, Width=460,
                MonitorHeight=400, SummaryColumns=2, GpuColumns=2 },
            "ribbon" => profile with { Edge=source.EdgePosition.IsHorizontal() ? source.EdgePosition : EdgePosition.Top, TopWidth=1520, TopHeight=280,
                MonitorWidth=420, SummaryColumns=2, GpuColumns=2 },
            _ => throw new ArgumentOutOfRangeException(nameof(id), "Bố cục mẫu không tồn tại.")
        };
        return profile.ApplyTo(source);
    }
}
