using Nepply.Models;

namespace Nepply.Modules.Panel;

/// <summary>Physical placement and DIP sizing, with no window or OS ownership.</summary>
internal readonly record struct PanelPlacement(int X, int Y, int PixelWidth, int PixelHeight, double Scale)
{
    internal double Width => PixelWidth / Scale;
    internal double Height => PixelHeight / Scale;
}

internal static class PanelGeometry
{
    internal const double EdgeGap = 12;

    internal static PanelPlacement Calculate(int left, int top, int screenWidth, int screenHeight,
        double scale, EdgePosition edge, double sideWidth, double topWidth, double topHeight, bool editing)
    {
        if (screenWidth <= 0 || screenHeight <= 0 || !double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(screenWidth), "Usable screen bounds and scale are required.");
        if (!Enum.IsDefined(edge)) throw new ArgumentOutOfRangeException(nameof(edge));
        if (!double.IsFinite(sideWidth) || !double.IsFinite(topWidth) || !double.IsFinite(topHeight)
            || sideWidth <= 0 || topWidth <= 0 || topHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(sideWidth));

        // Collapse the inset on exceptionally small work areas instead of placing
        // the only visible pixel outside them. Round once in physical coordinates.
        int insetX = (int)Math.Min(Math.Round(EdgeGap * scale), (screenWidth - 1) / 2);
        int insetY = (int)Math.Min(Math.Round(EdgeGap * scale), (screenHeight - 1) / 2);
        int availableWidth = screenWidth - 2 * insetX;
        int availableHeight = screenHeight - 2 * insetY;
        int width = (int)Math.Clamp(Math.Round((edge.IsHorizontal() ? topWidth : sideWidth) * scale), 1, availableWidth);
        int height = edge.IsHorizontal()
            ? (int)Math.Clamp(Math.Round((editing ? Math.Max(400, topHeight) : topHeight) * scale), 1, availableHeight)
            : availableHeight;
        int x = edge switch
        {
            EdgePosition.Right => left + screenWidth - insetX - width,
            EdgePosition.Top or EdgePosition.Bottom => left + (screenWidth - width) / 2,
            _ => left + insetX
        };
        int y = edge == EdgePosition.Bottom ? top + screenHeight - insetY - height : top + insetY;
        return new(x, y, width, height, scale);
    }

    internal static double MonitorWidth(bool visible, double preferredWidth, double availableWidth)
    {
        if (!visible || !double.IsFinite(availableWidth) || !double.IsFinite(preferredWidth)) return 0;
        return Math.Min(Math.Max(0, preferredWidth), Math.Max(0, availableWidth) * .4);
    }
}
