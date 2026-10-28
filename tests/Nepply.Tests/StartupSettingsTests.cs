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
        }
        finally { window.Close(); }
    }
}
