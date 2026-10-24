using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Nepply.Models;

namespace Nepply.Services;

/// <summary>Registers the global shortcut set on one window; each action gets its own hotkey id.</summary>
public class GlobalHotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int BaseId = 0x4862; // "Hb"; + (int)ShortcutAction
    private const uint NoRepeat = 0x4000;

    private HwndSource? _hwndSource;
    private readonly IntPtr _hwnd;
    private readonly Action<ShortcutAction> _onHotkey;
    private bool _isDisposed;
    private readonly Dictionary<ShortcutAction, ShortcutGesture> _registered = [];

    public GlobalHotkeyService(IntPtr hwnd, Action<ShortcutAction> onHotkey)
    {
        _hwnd = hwnd;
        _onHotkey = onHotkey;
    }

    /// <summary>
    /// Replaces the registered set with <paramref name="bindings"/> (empty gestures are skipped)
    /// and returns the actions Windows refused, e.g. because another app owns the key.
    /// </summary>
    public IReadOnlyList<ShortcutAction> Register(IReadOnlyDictionary<ShortcutAction, ShortcutGesture> bindings)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        var wanted = bindings.Where(b => !b.Value.IsEmpty).ToDictionary(b => b.Key, b => b.Value);
        if (_hwndSource != null && wanted.Count == _registered.Count && wanted.All(w => _registered.TryGetValue(w.Key, out var g) && g == w.Value))
            return [];
        ReleaseRegistration();
        if (_hwnd == IntPtr.Zero) return wanted.Keys.ToArray();
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        if (_hwndSource == null || _hwndSource.IsDisposed) { _hwndSource = null; return wanted.Keys.ToArray(); }
        _hwndSource.AddHook(WndProc);
        var failed = new List<ShortcutAction>();
        foreach (var (action, gesture) in wanted)
        {
            if (RegisterHotKey(_hwnd, BaseId + (int)action, (uint)gesture.Modifiers | NoRepeat, (uint)gesture.Key))
                _registered[action] = gesture;
            else
            {
                int error = Marshal.GetLastWin32Error();
                failed.Add(action);
                AppLogger.Info($"Global hotkey registration failed ({action}: modifiers={gesture.Modifiers}, key={gesture.Key}, Win32={error}).");
            }
        }
        return failed;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (_isDisposed || msg != WM_HOTKEY) return IntPtr.Zero;
        long id = wParam.ToInt64() - BaseId;
        if (id < 0 || id > int.MaxValue || !Enum.IsDefined((ShortcutAction)(int)id) || !_registered.ContainsKey((ShortcutAction)(int)id)) return IntPtr.Zero;
        handled = true;
        _onHotkey((ShortcutAction)(int)id);
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
        foreach (var action in _registered.Keys) UnregisterHotKey(_hwnd, BaseId + (int)action);
        _registered.Clear();
        if (_hwndSource is { IsDisposed: false }) _hwndSource.RemoveHook(WndProc);
        _hwndSource = null;
    }
}
