using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Nepply.Helpers;

// MenuItem objects describe commands only; Windows owns rendering and input.
internal static class WindowsPopupMenu
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenuW(IntPtr menu, uint flags, UIntPtr id, string? text);

    public static void Show(ContextMenu commands, Window owner, Point? anchor = null)
    {
        var menu = NativeInterop.CreatePopupMenu();
        if (menu == IntPtr.Zero) throw new Win32Exception();
        var actions = new Dictionary<int, MenuItem>();
        int selected;
        try
        {
            foreach (var entry in commands.Items)
            {
                if (entry is Separator) { AppendMenuW(menu, 0x800, UIntPtr.Zero, null); continue; }
                if (entry is not MenuItem item) continue;
                int id = actions.Count + 1;
                uint flags = (item.IsChecked ? 0x8u : 0) | (item.IsEnabled ? 0 : 0x3u);
                if (!AppendMenuW(menu, flags, (UIntPtr)id, item.Header?.ToString()?.Replace("&", "&&")))
                    throw new Win32Exception();
                actions.Add(id, item);
            }
            var handle = new WindowInteropHelper(owner).EnsureHandle();
            if (!NativeInterop.GetCursorPos(out var position)) throw new Win32Exception();
            if (anchor is { } point) { position.X = (int)point.X; position.Y = (int)point.Y; }
            selected = PopupMenuTracking.Track(handle,
                () => NativeInterop.TrackPopupMenuEx(menu, 0x100 | 0x2, position.X, position.Y, handle, IntPtr.Zero));
        }
        finally { NativeInterop.DestroyMenu(menu); }
        if (actions.TryGetValue(selected, out var action) && action.IsEnabled)
            action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, action));
    }
}
