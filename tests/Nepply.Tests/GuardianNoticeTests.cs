using System.IO;
using System.Windows;
using System.Windows.Controls;
using Nepply;
using Nepply.Models;
using Nepply.Services;

// F-06 at the panel: fixture Desktop and fixture guardian, so Explorer is never touched.
internal static class GuardianNoticeTests
{
    internal static void Run(string output, Action<string, bool> check, Action<int> pump)
    {
        string root = Path.Combine(output, "guardian-notice-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Exited(root, "DesktopGuardianExitedNotice", restoreFails: false, check, pump);
        Exited(root, "DesktopGuardianRecoveryFailedNotice", restoreFails: true, check, pump);
    }

    private static void Exited(string root, string key, bool restoreFails, Action<string, bool> check, Action<int> pump)
    {
        var desktop = new GuardedDesktopVisibilityTests.Desktop();
        GuardedDesktopVisibilityTests.Guardian? guardian = null;
        var lease = new DesktopVisibilityLease(desktop, Path.Combine(root, key + ".lease"));
        var flow = new GuardedDesktopVisibility(desktop, lease,
            () => Task.FromResult<IDesktopGuardianSession>(guardian = new(desktop)), static (_, _) => { });
        var icons = new DesktopIconService(flow, lease, _ => Task.FromResult(new List<DesktopIcon>()));
        var main = new MainWindow(preview: true, icons: icons) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        string language = LocalizationService.CurrentLanguage;
        try
        {
            main.Show(); pump(20);
            var panel = main.IconPanelView;
            var notice = (FrameworkElement)panel.FindName("LaunchNotice");
            var text = (TextBlock)panel.FindName("LaunchNoticeText");
            check($"Guardian fixture {key} starts hidden with no notice",
                icons.SetDesktopIconsHiddenAsync(true).GetAwaiter().GetResult() == null && icons.AreDesktopIconsHidden && notice.Visibility == Visibility.Collapsed);

            desktop.FailShow = restoreFails;
            guardian!.ExitCode = -1;
            guardian.Exit.TrySetResult();
            flow.RecoveryObservation.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            pump(20);
            check($"Guardian exit shows {key} on the panel", panel.ShowsActionNotice(key) && notice.Visibility == Visibility.Visible);
            check($"Guardian exit {key} keeps hidden state honest", icons.AreDesktopIconsHidden == restoreFails);

            var seen = new HashSet<string>();
            foreach (var option in LocalizationService.Languages)
            {
                LocalizationService.Apply(option.Id); pump(10);
                check($"Language {option.Id} renders {key}", text.Text == LocalizationService.Get(key) && text.Text != key);
                seen.Add(text.Text);
            }
            check($"{key} has its own text in all 8 languages", LocalizationService.Languages.Count == 8 && seen.Count == 8);
            LocalizationService.Apply(language); pump(10);

            desktop.FailShow = false;
            if (!restoreFails)
            {
                var hideAgain = main.ApplyDesktopVisibilityAsync(true);
                for (int attempt = 0; attempt < 500 && !hideAgain.IsCompleted; attempt++) pump(10);
                check("Hiding again dismisses the guardian notice",
                    hideAgain.IsCompletedSuccessfully && desktop.Visible == false && !panel.ShowsActionNotice(key) && notice.Visibility == Visibility.Collapsed);
            }
        }
        finally
        {
            desktop.FailShow = false;
            LocalizationService.Apply(language);
            main.Cleanup(); main.Close();
        }
    }
}
