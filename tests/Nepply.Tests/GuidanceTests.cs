using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nepply;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;

/// <summary>Usage guidance lives in the welcome guide; settings, tray and panel show feature names.</summary>
internal static class GuidanceTests
{
    private static readonly string[] RemovedHints = ["PreviewNotice", "MotionHint", "MetalHint", "GridHint", "GpuHint", "SuspendHint", "EcoHint",
        "ShortcutsHint", "PresetHint", "ExportHint", "PrivacyHint", "MergeHint", "EditTip", "LayoutEditingHint", "TrayExitHint", "TrayLogsHint",
        "TraySettingsHint", "LayoutDecisionHint"];

    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var node in Tree(child)) yield return node;
    }

    internal static void Run(Action<string,bool> check, Action<int> pump, string output, Action<FrameworkElement,int,int,string> render)
    {
        LocalizationService.Apply("vi");
        check("Explanatory hint texts are gone from the language resources", RemovedHints.All(key => !LocalizationService.TryGet(key, out _)));

        var settingsWindow = new SettingsWindow(new AppSettings(), _ => { });
        var captions = Tree(settingsWindow).OfType<TextBlock>().Select(t => t.Text).ToArray();
        check("Settings keeps its warnings: sensor limits and Desktop icon recovery",
            captions.Contains(LocalizationService.Get("SensorHint")) && captions.Contains(LocalizationService.Get("DesktopRestore")));
        check("Settings offers the guide next to update checking", settingsWindow.FindName("GuideButton") is Button { Content: string text } && text == LocalizationService.Get("GuideOpen"));
        check("Merge shortcuts option has no explanatory tooltip", ((CheckBox)settingsWindow.FindName("MergeShortcuts")).ToolTip == null);
        settingsWindow.Close();

        var settings = new AppSettings();
        ShortcutBindings.Set(settings, ShortcutAction.RefreshDesktop, new(0, 0x74));
        ShortcutBindings.Set(settings, ShortcutAction.HidePanel, ShortcutGesture.None);
        var lines = WelcomeWindow.ShortcutLines(settings);
        check("Guide lists every assigned shortcut with its current key and skips unassigned ones",
            lines.Contains(LocalizationService.Get("ShortcutFocusSearch") + " · Ctrl + F")
            && lines.Contains(LocalizationService.Get("ShortcutRefreshDesktop") + " · F5")
            && !lines.Any(line => line.StartsWith(LocalizationService.Get("ShortcutHidePanel")))
            && !lines.Any(line => line.StartsWith(LocalizationService.Get("ShortcutTogglePanel"))));

        var firstRun = new WelcomeWindow(new AppSettings());
        check("First-run welcome still asks for dock side and Desktop icons",
            ((FrameworkElement)firstRun.FindName("SetupArea")).Visibility == Visibility.Visible
            && Equals(((Button)firstRun.FindName("StartButton")).Content, LocalizationService.Get("WelcomeStart")));
        firstRun.Close();
        var guide = new WelcomeWindow(settings, guideOnly: true);
        check("Reopened guide hides first-run choices and closes instead of starting",
            guide.IsGuide && ((FrameworkElement)guide.FindName("SetupArea")).Visibility == Visibility.Collapsed
            && Equals(((Button)guide.FindName("StartButton")).Content, LocalizationService.Get("Close"))
            && ((ItemsControl)guide.FindName("ShortcutList")).Items.Count == lines.Length);
        render((FrameworkElement)guide.Content, 560, 760, Path.Combine(output, "welcome-guide.png"));
        typeof(WelcomeWindow).GetMethod("Start_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(guide, [guide, new RoutedEventArgs()]);
        check("Closing the guide does not complete onboarding", !settings.HasCompletedOnboarding);
        guide.Close();

        var decision = new LayoutDecisionWindow();
        check("Layout decision dialog asks its question without an explanation line",
            !Tree(decision).OfType<TextBlock>().Any(t => t.Foreground == Application.Current.Resources["MutedBrush"]));
        decision.Close();

        var icons = new DesktopIconService(_ => Task.FromResult(new List<DesktopIcon>()));
        var main = new MainWindow(preview: true, icons: icons) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        try
        {
            main.Show(); pump(10);
            var tray = new TrayMenu(main, () => { }, () => { });
            check("Tray menu items are names only and include the guide",
                tray.FindName("GuideItem") is MenuItem
                && tray.Items.OfType<MenuItem>().Where(i => i.IsEnabled).All(i => i.Header is string || i.Header is TextBlock));
            check("Edit layout tooltip is the action name", Equals(((FrameworkElement)main.FindName("EditLayoutButton")).ToolTip, LocalizationService.Get("EditLayout")));
        }
        finally { main.Cleanup(); main.Close(); }
    }
}
