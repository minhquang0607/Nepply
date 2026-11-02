using System.Windows.Controls;
using Nepply.Models;
using Nepply.Views;

internal static class StartupSettingsTests
{
    internal static void Run(Action<string, bool> check)
    {
        var window = new SettingsWindow(new AppSettings { AutoStart = false }, _ => { });
        try
        {
            var checkbox = (CheckBox)window.FindName("AutoStart");
            int revision = window.AutoStartRevision;
            window.SynchronizeAutoStart(false, true, revision);
            check("Startup OS result updates unchanged settings checkbox", checkbox.IsChecked == true);
            check("Startup programmatic synchronization is not a user edit", window.AutoStartRevision == revision);
            checkbox.IsChecked = false;
            checkbox.IsChecked = true;
            window.SynchronizeAutoStart(true, false, revision);
            check("Startup preserves newer checkbox edit even after toggle-back", checkbox.IsChecked == true && window.AutoStartRevision == revision + 2);
            window.SynchronizeAutoStart(true, false, window.AutoStartRevision);
            check("Startup current result can correct checkbox after rejection", checkbox.IsChecked == false);
            var notice = (TextBlock)window.FindName("AutoStartWindowsNotice");
            window.ShowAutoStartState(true);
            check("Unticked startup has nothing to warn about", notice.Visibility != System.Windows.Visibility.Visible);
            checkbox.IsChecked = true;
            window.ShowAutoStartState(true);
            check("Ticked startup says when Windows has it off", notice.Visibility == System.Windows.Visibility.Visible);
            window.ShowAutoStartState(false);
            check("The startup warning leaves once Windows has it on again", notice.Visibility != System.Windows.Visibility.Visible);
        }
        finally { window.Close(); }
    }
}
