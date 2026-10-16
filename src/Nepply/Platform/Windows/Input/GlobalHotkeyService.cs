using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Nepply.Services;

public class GlobalHotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int HOTKEY_ID = 0x4862; // "Hb"
    private const uint MOD_CONTROL = 0x0002;
    private const uint VK_SPACE = 0x20;
    
    private HwndSource? _hwndSource;
    private readonly IntPtr _hwnd;
    private readonly Action _onToggle;
    private bool _isDisposed;
    private bool _registered;
    private uint _modifiers, _key;

    public GlobalHotkeyService(IntPtr hwnd, Action onToggle)
    {
        _hwnd = hwnd;
        _onToggle = onToggle;
    }

    public bool Register(uint modifiers = MOD_CONTROL, uint key = VK_SPACE)
    {
        ObjectDisposedException.ThrowIf(_isDisposed,this);
        if(_registered && _modifiers==modifiers && _key==key) return true;
        ReleaseRegistration();
        if(_hwnd==IntPtr.Zero) return false;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        if(_hwndSource==null || _hwndSource.IsDisposed) { _hwndSource=null; return false; }
        _hwndSource.AddHook(WndProc);
        
        if (!RegisterHotKey(_hwnd, HOTKEY_ID, modifiers | 0x4000, key))
        {
            int error=Marshal.GetLastWin32Error();
            ReleaseRegistration();
            AppLogger.Info($"Global hotkey registration failed (modifiers={modifiers}, key={key}, Win32={error}).");
            return false;
        }
        _modifiers=modifiers; _key=key; _registered=true;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (!_isDisposed && _registered && msg == WM_HOTKEY && wParam.ToInt64() == HOTKEY_ID)
        {
            handled = true;
            _onToggle();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        ReleaseRegistration();
    }
    private void ReleaseRegistration()
    {
        bool wasRegistered=_registered;
        _registered=false;
        if (wasRegistered && _hwnd != IntPtr.Zero)
            UnregisterHotKey(_hwnd, HOTKEY_ID);

        if(_hwndSource is {IsDisposed:false}) _hwndSource.RemoveHook(WndProc);
        _hwndSource = null;
    }
}
