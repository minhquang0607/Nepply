using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Nepply;
using Nepply.Models;
using Nepply.Services;

internal static class GlobalShortcutTests
{
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    private const int WM_HOTKEY = 0x0312, BaseId = 0x4862;

    internal static void Run(Action<string, bool> check, Action<int> pump)
    {
        using (var invalid = new GlobalHotkeyService(IntPtr.Zero, _ => throw new Exception("Unexpected callback")))
        {
            var refused = invalid.Register(new Dictionary<ShortcutAction, ShortcutGesture> { [ShortcutAction.TogglePanel] = new(2, 0x20) });
            check("Hotkey without window is rejected without native registration", refused.SequenceEqual([ShortcutAction.TogglePanel]));
            invalid.Dispose(); invalid.Dispose();
            bool disposedRejected = false;
            try { invalid.Register(new Dictionary<ShortcutAction, ShortcutGesture>()); } catch (ObjectDisposedException) { disposedRejected = true; }
            check("Disposed hotkey cannot register again", disposedRejected);
        }

        // Ctrl+Alt+Shift+F11/F12 are practically never owned by other apps.
        var pin = new ShortcutGesture(7, 0x7A); var settings = new ShortcutGesture(7, 0x7B);
        using var first = new HwndSource(new HwndSourceParameters("Nepply hotkey fixture A") { Width = 1, Height = 1, WindowStyle = 0 });
        using var second = new HwndSource(new HwndSourceParameters("Nepply hotkey fixture B") { Width = 1, Height = 1, WindowStyle = 0 });
        var fired = new List<ShortcutAction>();
        using (var service = new GlobalHotkeyService(first.Handle, fired.Add))
        using (var rival = new GlobalHotkeyService(second.Handle, _ => { }))
        {
            var failed = service.Register(new Dictionary<ShortcutAction, ShortcutGesture>
            { [ShortcutAction.TogglePin] = pin, [ShortcutAction.OpenSettings] = settings, [ShortcutAction.SearchDesktop] = ShortcutGesture.None });
            check("Several global shortcuts register on one window", failed.Count == 0);
            SendMessage(first.Handle, WM_HOTKEY, (IntPtr)(BaseId + (int)ShortcutAction.TogglePin), IntPtr.Zero);
            SendMessage(first.Handle, WM_HOTKEY, (IntPtr)(BaseId + (int)ShortcutAction.OpenSettings), IntPtr.Zero);
            SendMessage(first.Handle, WM_HOTKEY, (IntPtr)(BaseId + (int)ShortcutAction.SearchDesktop), IntPtr.Zero);
            SendMessage(first.Handle, WM_HOTKEY, (IntPtr)(BaseId + 999), IntPtr.Zero);
            check("Each hotkey reports its own action and unassigned ids are ignored",
                fired.SequenceEqual([ShortcutAction.TogglePin, ShortcutAction.OpenSettings]));
            var taken = rival.Register(new Dictionary<ShortcutAction, ShortcutGesture> { [ShortcutAction.TogglePin] = pin });
            check("A key already owned elsewhere is reported as failed", taken.SequenceEqual([ShortcutAction.TogglePin]));
            service.Register(new Dictionary<ShortcutAction, ShortcutGesture> { [ShortcutAction.OpenSettings] = settings });
            check("Re-registering releases shortcuts that were removed",
                rival.Register(new Dictionary<ShortcutAction, ShortcutGesture> { [ShortcutAction.TogglePin] = pin }).Count == 0);
            fired.Clear();
            SendMessage(first.Handle, WM_HOTKEY, (IntPtr)(BaseId + (int)ShortcutAction.TogglePin), IntPtr.Zero);
            check("A released shortcut no longer reaches the old window", fired.Count == 0);
        }

        var icons = new DesktopIconService(_ => Task.FromResult(new List<DesktopIcon>()));
        var main = new MainWindow(preview: true, icons: icons) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        try
        {
            main.Show(); pump(20);
            bool wasPinned = main.IsPinned;
            main.RunGlobalShortcut(ShortcutAction.TogglePin); pump(20);
            check("Pin shortcut toggles pinning", main.IsPinned != wasPinned);
            main.RunGlobalShortcut(ShortcutAction.TogglePin); pump(20);
            bool hidden = main.Settings.HideDesktopIcons;
            main.RunGlobalShortcut(ShortcutAction.ToggleDesktopIcons); pump(20);
            check("Desktop icon shortcut flips the saved option", main.Settings.HideDesktopIcons != hidden);
            main.RunGlobalShortcut(ShortcutAction.ToggleDesktopIcons); pump(20);
            main.RunGlobalShortcut(ShortcutAction.SearchDesktop); pump(40);
            check("Search shortcut opens the Desktop search", ((FrameworkElement)main.FindName("DesktopSearchEditor")).Visibility == Visibility.Visible);
            var icons2 = (Nepply.Views.IconPanel)main.FindName("IconPanelView");
            var noticeText = (System.Windows.Controls.TextBlock)icons2.FindName("LaunchNoticeText");
            main.PresentShortcutFailures([new ShortcutGesture(3, 0x44)]);
            check("Refused global shortcuts are reported by key",
                noticeText.Text == string.Format(LocalizationService.Get("ShortcutUnavailableNotice"), "Ctrl + Alt + D"));
            main.PresentShortcutFailures([]);
            check("A later successful registration dismisses that notice",
                ((FrameworkElement)icons2.FindName("LaunchNotice")).Visibility == Visibility.Collapsed);
        }
        finally { main.Cleanup(); main.Close(); }
        ClosedPanelTimers(check, pump);
    }

    // CI regression: closing a shown panel raised Deactivated after Cleanup, which restarted the
    // auto-hide timer; its tick then mapped screen points on a closed window and crashed.
    private static void ClosedPanelTimers(Action<string, bool> check, Action<int> pump)
    {
        var icons = new DesktopIconService(_ => Task.FromResult(new List<DesktopIcon>()));
        var main = new MainWindow(preview: true, icons: icons) { ShowInTaskbar = false, Left = -10000, Top = -10000 };
        main.Show(); main.ShowPanel(); pump(20);
        main.Cleanup(); main.Close(); pump(20);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(MainWindow).GetMethod("ScheduleHide", flags)!.Invoke(main, null);
        var timer = (System.Windows.Threading.DispatcherTimer)typeof(MainWindow).GetField("_hideTimer", flags)!.GetValue(main)!;
        check("A closed panel never restarts its auto-hide timer", !timer.IsEnabled);
        bool threw = false;
        try { typeof(MainWindow).GetMethod("HideTick", flags)!.Invoke(main, [timer, EventArgs.Empty]); }
        catch (System.Reflection.TargetInvocationException) { threw = true; }
        check("An auto-hide tick after closing is ignored instead of crashing", !threw && !timer.IsEnabled);
    }
}
