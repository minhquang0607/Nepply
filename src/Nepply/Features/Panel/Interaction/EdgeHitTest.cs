using Nepply.Models;

namespace Nepply.Modules.Panel;

/// <summary>Physical work-area coordinates; never captures taskbar clicks.</summary>
internal static class EdgeHitTest
{
    internal static bool Contains(EdgePosition edge, int x, int y, int left, int top, int right, int bottom, int size)
    {
        if (size <= 0 || x < left || x >= right || y < top || y >= bottom) return false;
        return edge switch
        {
            EdgePosition.Left => x - left < size,
            EdgePosition.Right => right - x <= size,
            EdgePosition.Top => y - top < size,
            EdgePosition.Bottom => bottom - y <= size,
            _ => false
        };
    }
    // True past the end of the edge too, such as over the taskbar in a corner.
    internal static bool BesideEdge(EdgePosition edge, int x, int y, int left, int top, int right, int bottom, int size)
    {
        int distance = edge switch
        {
            EdgePosition.Left => x - left,
            EdgePosition.Right => right - 1 - x,
            EdgePosition.Top => y - top,
            EdgePosition.Bottom => bottom - 1 - y,
            _ => -1
        };
        return distance >= 0 && distance < size;
    }
    // Pixels from the pointer to the trigger edge; null outside the monitor.
    internal static int? DistanceToEdge(EdgePosition edge, int x, int y, int left, int top, int right, int bottom)
    {
        if (x < left || x >= right || y < top || y >= bottom) return null;
        return edge switch
        {
            EdgePosition.Left => x - left,
            EdgePosition.Right => right - 1 - x,
            EdgePosition.Top => y - top,
            EdgePosition.Bottom => bottom - 1 - y,
            _ => null
        };
    }
}

/// <summary>Edge polling cadence: responsive near the trigger edge or while the panel is up, slow elsewhere to let the CPU idle.</summary>
internal static class EdgePolling
{
    // Fine enough to end the trigger delay on time; the pointer stays this close only briefly.
    internal static readonly TimeSpan NearEdge = TimeSpan.FromMilliseconds(40);
    internal static readonly TimeSpan Responsive = TimeSpan.FromMilliseconds(80);
    internal static readonly TimeSpan Idle = TimeSpan.FromMilliseconds(125);
    // Wide enough that even a fast approach is sampled several times before the zone.
    internal const double NearBandDip = 160;

    internal static TimeSpan Interval(bool panelActive, int? distanceToEdge, int nearBand)
        => panelActive ? Responsive : distanceToEdge is { } distance && distance <= nearBand ? NearEdge : Idle;
}
