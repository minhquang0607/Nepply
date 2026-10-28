using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Nepply;
using Nepply.Models;
using Nepply.Services;

internal static class PanelShortcutTests
{
    private const int F5 = 0x74, S = 0x53, F = 0x46, Esc = 0x1B;
    internal static void Run(Action<string, bool> check, Action<int> pump)
    {
        int scans = 0;
        var icons = new DesktopIconService(_ => { Interlocked.Increment(ref scans); return Task.FromResult(new List<DesktopIcon>()); });
        var main = new MainWindow(preview: true, icons: icons) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        try
        {
            main.Show(); pump(20);
            var source = HwndSource.FromHwnd(new WindowInteropHelper(main).EnsureHandle());
            var editor = (FrameworkElement)main.FindName("DesktopSearchEditor");
            void Press(Key key, ModifierKeys modifiers)
                => main.HandlePanelKey(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent }, Nepply.Helpers.ShortcutInput.FromKey(key, Key.None, modifiers));
            void CloseSearch() { Press(Key.Escape, ModifierKeys.None); pump(400); } // The collapse animates for about 160 ms.

            check("Key events convert to stored gestures, ignoring lone modifiers and Windows-key chords",
                Nepply.Helpers.ShortcutInput.FromKey(Key.F, Key.None, ModifierKeys.Control) == new ShortcutGesture(2, F)
                && Nepply.Helpers.ShortcutInput.FromKey(Key.System, Key.S, ModifierKeys.Alt) == new ShortcutGesture(1, S)
                && Nepply.Helpers.ShortcutInput.FromKey(Key.LeftCtrl, Key.None, ModifierKeys.Control).IsEmpty
                && Nepply.Helpers.ShortcutInput.FromKey(Key.D, Key.None, ModifierKeys.Windows).IsEmpty);

            Press(Key.F, ModifierKeys.Control); pump(30);
            check("Default Ctrl+F opens the panel search", editor.Visibility == Visibility.Visible);
            CloseSearch();
            check("Esc closes the empty search before hiding the panel", editor.Visibility == Visibility.Collapsed);

            ShortcutBindings.Set(main.Settings, ShortcutAction.FocusSearch, new(3, S));
            ShortcutBindings.Set(main.Settings, ShortcutAction.RefreshDesktop, new(0, F5));
            main.ApplySettings();
            Press(Key.F, ModifierKeys.Control); pump(30);
            check("A rebound search key replaces Ctrl+F", editor.Visibility == Visibility.Collapsed);
            Press(Key.S, ModifierKeys.Control | ModifierKeys.Alt); pump(30);
            check("The new search key opens the panel search", editor.Visibility == Visibility.Visible);
            CloseSearch();

            main.ShowPanel(); pump(30);
            Press(Key.Escape, ModifierKeys.None); pump(30);
            check("With nothing to back out of, Esc hides the panel", !main.IsPanelShown);

            int before = Volatile.Read(ref scans);
            Press(Key.F5, ModifierKeys.None);
            check("A refresh key, once assigned, rescans the Desktop", SpinWait.SpinUntil(() => { pump(5); return Volatile.Read(ref scans) > before; }, 3000));

            LocalizationService.Apply("vi"); main.ApplySettings();
            var searchButton = (FrameworkElement)main.FindName("DesktopSearchButton");
            var refreshButton = (FrameworkElement)main.FindName("RefreshDesktopButton");
            check("Header tooltips show each action's current key",
                Equals(searchButton.ToolTip, LocalizationService.Get("SearchDesktopTip") + " · Ctrl + Alt + S")
                && Equals(refreshButton.ToolTip, LocalizationService.Get("Refresh") + " · F5"));
            ShortcutBindings.Set(main.Settings, ShortcutAction.RefreshDesktop, ShortcutGesture.None); main.ApplySettings();
            check("Tooltips drop the key when an action is unassigned", Equals(refreshButton.ToolTip, LocalizationService.Get("Refresh")));
        }
        finally { main.Cleanup(); main.Close(); }
    }
}
