using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Nepply.Services;

public interface IDesktopVisibility
{
    bool? IsVisible();
    bool SetVisible(bool visible);
}

// Shared source with the guardian: no WPF or monitor dependency.
internal sealed class ExplorerDesktopVisibility : IDesktopVisibility
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string name, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string name, string? title);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);
    internal static (IntPtr View, IntPtr List) Locate()
    {
        var view = FindWindowEx(FindWindow("Progman", null), IntPtr.Zero, "SHELLDLL_DefView", null);
        for (IntPtr worker = IntPtr.Zero; view == IntPtr.Zero;)
        {
            worker = FindWindowEx(IntPtr.Zero, worker, "WorkerW", null);
            if (worker == IntPtr.Zero) break;
            view = FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
        }
        return (view, view == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(view, IntPtr.Zero, "SysListView32", null));
    }
    public bool? IsVisible() { var (_, list) = Locate(); return list == IntPtr.Zero ? null : IsWindowVisible(list); }
    public bool SetVisible(bool visible)
    {
        // Explorer exposes a toggle, not an idempotent setter. The app's local
        // fallback and the guardian must not both observe "hidden" then toggle.
        // Separate from the generation fence, which a guardian holds for life.
        using var identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ?? throw new UnauthorizedAccessException("Missing Desktop user identity.");
        using var mutex = new Mutex(false, @"Local\Nepply-DesktopVisibility-" + sid);
        bool acquired;
        try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(2)); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) return false;
        try { return SetVisibleUnderLock(visible); }
        finally { mutex.ReleaseMutex(); }
    }

    private bool SetVisibleUnderLock(bool visible)
    {
        var (view, list) = Locate();
        if (list == IntPtr.Zero) return false;
        if (IsWindowVisible(list) == visible) return true;
        if (SendMessageTimeout(view, 0x0111, (IntPtr)0x7402, IntPtr.Zero, 0x0002 | 0x0020, 500, out _) == IntPtr.Zero) return false;
        return IsVisible() == visible;
    }
}

// Store edition: hides the icon window itself instead of switching Explorer's saved
// option. Windows settings stay untouched, so a removed package leaves nothing behind
// and icons return with the next Explorer start.
internal sealed class WindowDesktopVisibility(Func<IntPtr> list, Func<IntPtr, bool> isVisible, Func<IntPtr, int, bool> show) : IDesktopVisibility
{
    private const int Hide = 0, ShowWithoutActivating = 8;
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    internal WindowDesktopVisibility() : this(() => ExplorerDesktopVisibility.Locate().List, IsWindowVisible, ShowWindow) { }

    public bool? IsVisible() { var window = list(); return window == IntPtr.Zero ? null : isVisible(window); }
    public bool SetVisible(bool visible)
    {
        var window = list();
        if (window == IntPtr.Zero) return false;
        // ShowWindow reports the previous state, so only the result below is meaningful.
        show(window, visible ? ShowWithoutActivating : Hide);
        return isVisible(window) == visible;
    }
}
