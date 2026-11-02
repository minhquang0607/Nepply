using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;

internal static class SettingsShortcutTests
{
    private const int Space = 0x20, D = 0x44, F5 = 0x74;
    internal static void Run(Action<string, bool> check, Action<int> pump)
    {
        foreach (var language in LocalizationService.Languages)
        {
            LocalizationService.Apply(language.Id);
            check($"Language {language.Id} names every shortcut action",
                ShortcutBindings.All.All(a => LocalizationService.TryGet(SettingsWindow.ShortcutLabelKey(a), out var text) && text.Length > 0));
        }
        LocalizationService.Apply("vi");
        var saved = new AppSettings();
        var persisted = new List<AppSettings>();
        var settings = new SettingsWindow(saved, persisted.Add) { Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            settings.Show(); pump(20);
            var source = HwndSource.FromHwnd(new WindowInteropHelper(settings).EnsureHandle());
            Button Row(string name) => (Button)settings.FindName(name);
            void Click(string handler, Button sender) => typeof(SettingsWindow).GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [sender, new RoutedEventArgs()]);
            void Press(Key key, ShortcutGesture gesture) => settings.RecordShortcut(
                new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent }, gesture);
            var error = (TextBlock)settings.FindName("ErrorText");
            bool recording() => (bool)typeof(SettingsWindow).GetField("_recording", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(settings)!;
            static bool Inside(DependencyObject child, DependencyObject ancestor)
            { for (var node = child; node != null; node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node)) if (node == ancestor) return true; return false; }

            settings.ShowSettingsSection(5); pump(20);
            var section = (FrameworkElement)settings.FindName("ShortcutsSection");
            check("Show/hide covers hiding, so there is no separate hide panel row", settings.FindName("ShortcutHidePanel") == null);
            check("Settings has a Shortcuts section holding every shortcut, including open/close",
                section.Visibility == Visibility.Visible && Inside(Row("HotkeyButton"), section)
                && !Inside(Row("HotkeyButton"), (DependencyObject)settings.FindName("PanelSection")));
            check("Shortcut rows show default keys and unassigned actions",
                Equals(Row("HotkeyButton").Content, "Ctrl + Alt + Space") && Equals(Row("ShortcutFocusSearch").Content, "Ctrl + F")
                && Equals(Row("ShortcutSearchDesktop").Content, LocalizationService.Get("ShortcutNone")));
            check("Every shortcut row has an accessible action name",
                ShortcutBindings.All.Where(a => a != ShortcutAction.TogglePanel)
                    .All(a => System.Windows.Automation.AutomationProperties.GetName(Row("Shortcut" + a)) == LocalizationService.Get("Shortcut" + a)));

            Click("Hotkey_Click", Row("ShortcutSearchDesktop"));
            check("Recording a global shortcut asks for Ctrl/Alt + a key",
                recording() && Equals(Row("ShortcutSearchDesktop").Content, LocalizationService.Get("RecordHotkeyPrompt")));
            Press(Key.F5, new(0, F5));
            check("A global key without Ctrl or Alt is refused and recording continues",
                recording() && error.Text == LocalizationService.Get("ShortcutInvalidGlobal"));
            Press(Key.D, new(3, D));
            check("A valid key is assigned to the draft and shown",
                !recording() && Equals(Row("ShortcutSearchDesktop").Content, "Ctrl + Alt + D") && error.Text == "");

            Click("Hotkey_Click", Row("ShortcutOpenSettings"));
            Press(Key.Space, new(3, Space));
            check("A key used by another action is refused with that action's name",
                recording() && error.Text == string.Format(LocalizationService.Get("ShortcutConflict"), "Ctrl + Alt + Space", LocalizationService.Get("ShortcutTogglePanel")));
            Press(Key.Escape, new(0, 0x1B));
            check("Esc cancels recording without changing the key",
                !recording() && Equals(Row("ShortcutOpenSettings").Content, LocalizationService.Get("ShortcutNone")));

            Click("Hotkey_Click", Row("ShortcutRefreshDesktop"));
            check("Recording a panel shortcut asks for any key", Equals(Row("ShortcutRefreshDesktop").Content, LocalizationService.Get("RecordShortcutPrompt")));
            Press(Key.F5, new(0, F5));
            check("A bare function key works for panel shortcuts", Equals(Row("ShortcutRefreshDesktop").Content, "F5"));

            var searchRow = (Grid)VisualTreeHelper.GetParent(Row("ShortcutFocusSearch"));
            Click("ShortcutClear_Click", Row("ShortcutFocusSearch"));
            check("Clearing a shortcut leaves it unassigned", Equals(Row("ShortcutFocusSearch").Content, LocalizationService.Get("ShortcutNone")));
            Click("ShortcutDefault_Click", Row("ShortcutFocusSearch"));
            check("The default button restores a cleared key", Equals(Row("ShortcutFocusSearch").Content, "Ctrl + F"));
            check("Open/close has no clear button",
                ((Grid)VisualTreeHelper.GetParent(Row("HotkeyButton"))).Children.OfType<Button>().Count() == 2 && searchRow.Children.OfType<Button>().Count() == 3);

            ((Button)settings.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); pump(10);
            check("Apply saves the edited shortcuts",
                ShortcutBindings.Get(saved, ShortcutAction.SearchDesktop) == new ShortcutGesture(3, D)
                && ShortcutBindings.Get(saved, ShortcutAction.RefreshDesktop) == new ShortcutGesture(0, F5) && persisted.Count > 0);

            Click("ResetShortcuts_Click", (Button)settings.FindName("ResetShortcuts"));
            check("Restore all brings back every default key",
                Equals(Row("ShortcutSearchDesktop").Content, LocalizationService.Get("ShortcutNone"))
                && Equals(Row("ShortcutRefreshDesktop").Content, LocalizationService.Get("ShortcutNone")) && Equals(Row("HotkeyButton").Content, "Ctrl + Alt + Space"));
            check("Settings search finds shortcut rows in the Shortcuts section",
                SettingsSearch.Find("phim tat").Any(e => e.Section == 5 && e.Control == "ShortcutTogglePin")
                && SettingsSearch.Find("phim tat").Any(e => e.Section == 5 && e.Control == "HotkeyButton"));
            LocalizationService.Apply("en");
            check("Shortcut search labels follow the language",
                SettingsSearch.Find("pin").Any(e => e.Control == "ShortcutTogglePin" && e.Label == LocalizationService.Get("ShortcutTogglePin")));
        }
        finally { settings.Close(); LocalizationService.Apply("vi"); }
    }
}
