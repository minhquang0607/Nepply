using System.Runtime.InteropServices;

namespace Nepply.Helpers;

/// <summary>
/// Shared TrackPopupMenuEx protocol for every native popup Nepply opens.
/// The dock is shown without activation, and Windows only dismisses a popup on
/// an outside click (and routes keys to it) when its owner is the foreground
/// window. The WM_NULL afterwards lets the menu loop finish closing (KB135788).
/// </summary>
internal static class PopupMenuTracking
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    internal static int Track(IntPtr owner, Func<int> track) =>
        Track(owner, track, window => SetForegroundWindow(window), window => PostMessageW(window, 0, IntPtr.Zero, IntPtr.Zero));

    internal static int Track(IntPtr owner, Func<int> track, Action<IntPtr> setForeground, Action<IntPtr> postNull)
    {
        ArgumentNullException.ThrowIfNull(track);
        setForeground(owner);
        try { return track(); }
        finally { postNull(owner); }
    }
}
