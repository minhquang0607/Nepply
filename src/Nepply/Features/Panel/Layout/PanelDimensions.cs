using Nepply.Helpers;
using Nepply.Models;

namespace Nepply.Modules.Panel;

internal static class PanelDimensions
{
    internal static (double Width, double Height) Available(DisplayScreen screen) =>
        (Available(screen.Width, screen.Scale), Available(screen.Height, screen.Scale));

    private static double Available(int pixels, double scale) =>
        (pixels - 2 * Math.Min(Math.Round(PanelGeometry.EdgeGap * scale), (pixels - 1) / 2)) / scale;

    internal static double Resolve(double percent, double legacy, double available, double minimum) =>
        Math.Clamp(percent > 0 ? available * percent / 100 : legacy, Math.Min(minimum, available), available);

    internal static (double Side, double Top, double Height) Resolve(AppSettings settings, DisplayScreen screen)
    {
        var (width, height) = Available(screen);
        return (Resolve(settings.PanelWidthPercent, settings.PanelWidth, width, 320),
            Resolve(settings.TopPanelWidthPercent, settings.TopPanelWidth, width, 720),
            Resolve(settings.PanelHeightPercent, settings.PanelHeight, height, 240));
    }

    internal static bool OnlyDimensionsChanged(AppSettings a, AppSettings b) =>
        Settings.SettingsChangeSet.Between(a,b).OnlyDimensions;

}
