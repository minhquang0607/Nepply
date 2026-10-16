using Nepply.Models;

namespace Nepply.Modules.Panel;

/// <summary>Physical work-area coordinates; never captures taskbar clicks.</summary>
internal static class EdgeHitTest
{
    internal static bool Contains(EdgePosition edge,int x,int y,int left,int top,int right,int bottom,int size)
    {
        if(size<=0 || x<left || x>=right || y<top || y>=bottom) return false;
        return edge switch
        {
            EdgePosition.Left => x-left<size,
            EdgePosition.Right => right-x<=size,
            EdgePosition.Top => y-top<size,
            EdgePosition.Bottom => bottom-y<=size,
            _ => false
        };
    }
}
