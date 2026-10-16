using Nepply.Models;

namespace Nepply.Modules.Desktop;

/// <summary>
/// User-initiated commands only. Native menu coordinates are physical screen
/// coordinates supplied by the Windows presentation adapter. Null means success
/// or user cancellation; non-null describes a failure rendered by the UI in its current language.
/// This Windows-specific boundary deliberately does not pretend to be portable.
/// </summary>
public interface IDesktopActions
{
    void Refresh();
    UserMessage? LaunchIcon(DesktopIcon icon);
    UserMessage? ShowShellContextMenu(DesktopIcon icon, IntPtr hwnd, System.Windows.Point? screenAnchor = null);
}
