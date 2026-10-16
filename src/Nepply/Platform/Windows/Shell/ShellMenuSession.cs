using System.Runtime.InteropServices;
using System.Windows.Interop;
using static Nepply.Helpers.NativeInterop;

namespace Nepply.Helpers;

// Interface aliases belong to the caller's RCW. Never release each alias separately.
internal sealed class ShellMenuSession : IDisposable
{
    private readonly HwndSource _source;
    private readonly IContextMenu2? _menu2;
    private readonly IContextMenu3? _menu3;
    private bool _disposed;

    internal ShellMenuSession(IntPtr owner, IContextMenu menu)
    {
        _source = HwndSource.FromHwnd(owner) ?? throw new InvalidOperationException("Shell menu requires a live WPF owner.");
        _menu3 = menu as IContextMenu3;
        if (_menu3 == null) _menu2 = menu as IContextMenu2;
        _source.AddHook(HandleMessage);
    }

    internal static bool IsMenuMessage(int message, IntPtr wParam) => message switch
    {
        0x0117 or 0x0120 => true, // WM_INITMENUPOPUP, WM_MENUCHAR
        0x002B or 0x002C => wParam == IntPtr.Zero, // DRAWITEM/MEASUREITEM: menus only, not controls
        _ => false
    };

    private IntPtr HandleMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_disposed || !IsMenuMessage(message, wParam)) return IntPtr.Zero;
        try
        {
            if (_menu3 != null)
            {
                int hr = _menu3.HandleMenuMsg2((uint)message, wParam, lParam, out var result);
                if (hr == 0) { handled = true; return result; }
            }
            else if (_menu2 != null && message != 0x0120 && _menu2.HandleMenuMsg((uint)message, wParam, lParam) == 0)
            {
                handled = true;
                return message is 0x002B or 0x002C ? new IntPtr(1) : IntPtr.Zero;
            }
        }
        catch (COMException ex) { Services.AppLogger.Error(ex, "Forward Shell menu message"); }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _source.RemoveHook(HandleMessage);
    }
}
