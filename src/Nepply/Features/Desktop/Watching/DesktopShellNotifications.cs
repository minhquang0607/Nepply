using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Nepply.Services;

/// <summary>Shell notifications complement folder watchers; periodic reconciliation remains the fallback.</summary>
internal sealed class DesktopShellNotifications : IDisposable
{
    private const int Message = 0x8000 + 173;
    private readonly HwndSource _source;
    private readonly Action<string[]?> _changed;
    private uint _registration;
    private IntPtr _desktop;
    [StructLayout(LayoutKind.Sequential)]
    private struct Entry { public IntPtr Pidl; [MarshalAs(UnmanagedType.Bool)] public bool Recursive; }
    [DllImport("shell32.dll")] private static extern int SHGetSpecialFolderLocation(IntPtr owner, int folder, out IntPtr pidl);
    [DllImport("shell32.dll")] private static extern uint SHChangeNotifyRegister(IntPtr window, int sources, int events, uint message, int count, ref Entry entry);
    [DllImport("shell32.dll")] private static extern bool SHChangeNotifyDeregister(uint registration);
    [DllImport("shell32.dll")] private static extern IntPtr SHChangeNotification_Lock(IntPtr change, uint process, out IntPtr pidls, out int events);
    [DllImport("shell32.dll")] private static extern bool SHChangeNotification_Unlock(IntPtr handle);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHGetPathFromIDListEx(IntPtr pidl, System.Text.StringBuilder path, uint size, uint flags);

    public DesktopShellNotifications(Window window, Action<string[]?> changed, string? root = null)
    {
        _changed = changed;
        _source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)
            ?? throw new InvalidOperationException("Desktop window handle is not ready.");
        try
        {
            Marshal.ThrowExceptionForHR(root == null ? SHGetSpecialFolderLocation(IntPtr.Zero, 0, out _desktop)
                : Helpers.NativeInterop.SHParseDisplayName(root, IntPtr.Zero, out _desktop, 0, out _));
            var entry = new Entry { Pidl = _desktop, Recursive = false };
            _source.AddHook(Hook);
            _registration = SHChangeNotifyRegister(_source.Handle, 0x8003, 0x7fffffff, Message, 1, ref entry);
            if (_registration == 0) throw new InvalidOperationException("Shell notifications could not be registered.");
        }
        catch { Dispose(); throw; }
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != Message || _registration == 0) return IntPtr.Zero;
        var locked = SHChangeNotification_Lock(wParam, unchecked((uint)lParam.ToInt64()), out var pidls, out var events);
        try
        {
            int count = PathCountForEvent(events);
            string[]? paths = null;
            if (locked != IntPtr.Zero && pidls != IntPtr.Zero && count > 0)
            {
                paths = new string[count];
                for (int i = 0; i < count; i++)
                {
                    var pidl = Marshal.ReadIntPtr(pidls, i * IntPtr.Size);
                    var path = new System.Text.StringBuilder(32768);
                    if (pidl == IntPtr.Zero || !SHGetPathFromIDListEx(pidl, path, (uint)path.Capacity, 0)) { paths = null; break; }
                    paths[i] = path.ToString();
                }
            }
            // Paths are copied while the shared PIDLs are locked; never retain or free those borrowed pointers.
            _changed(paths);
        }
        catch (Exception ex) { AppLogger.Error(ex, "Desktop Shell notification"); }
        finally { if (locked != IntPtr.Zero) SHChangeNotification_Unlock(locked); }
        handled = true;
        return IntPtr.Zero;
    }
    internal static int PathCountForEvent(int events) => (events & 0x7fffffff) switch
    {
        0x1 or 0x20000 => 2, // Rename item/folder: old and new identity.
        0x2 or 0x4 or 0x8 or 0x10 or 0x800 or 0x2000 => 1,
        _ => 0 // UPDATEDIR, associations, virtual items and unknown events need full repair.
    };
    public void Dispose()
    {
        if (_registration != 0) { SHChangeNotifyDeregister(_registration); _registration = 0; }
        _source.RemoveHook(Hook);
        if (_desktop != IntPtr.Zero) { Marshal.FreeCoTaskMem(_desktop); _desktop = IntPtr.Zero; }
    }
}
