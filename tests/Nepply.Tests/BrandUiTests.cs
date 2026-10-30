using System.Windows;
using System.Windows.Media.Imaging;
using Nepply;
using Nepply.Models;
using Nepply.Views;

internal static class BrandUiTests
{
    internal static void Run(Action<string, bool> check)
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Nepply;component/Resources/icon.ico"))!.Stream;
        var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        check("Brand icon includes all nine DPI sizes", decoder.Frames.Select(f => f.PixelWidth)
            .Order().SequenceEqual(new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 }));
        foreach (var window in new Window[] { new MainWindow(preview:true), new SettingsWindow(new AppSettings()),
            new WelcomeWindow(new AppSettings()), new SensorDiagnosticsWindow("Nepply"), new LayoutDecisionWindow() })
        {
            try
            {
                check("Brand icon resolves for " + window.GetType().Name, window.Icon != null);
                check("No stale title for " + window.GetType().Name, !window.Title.Contains("HoverBox", StringComparison.OrdinalIgnoreCase));
                if (window is SettingsWindow settings)
                {
                    var diagnostics = (FrameworkElement)settings.FindName("DeveloperDiagnostics");
#if DEBUG
                    check("Debug Settings retains diagnostics", diagnostics.Visibility == Visibility.Visible);
#else
                    check("Release Settings hides developer diagnostics", diagnostics.Visibility == Visibility.Collapsed);
#endif
                    var advanced = (System.Windows.Controls.Expander)settings.FindName("AdvancedMonitorOptions");
                    check("Advanced monitor starts collapsed", !advanced.IsExpanded);
                    var mode = (System.Windows.Controls.ComboBox)settings.FindName("GpuModeChoice");
                    var gpu = (FrameworkElement)settings.FindName("SelectedGpuOptions");
                    var integrated = (FrameworkElement)settings.FindName("SelectedIntegratedGpuOptions");
                    for (int selected = 0; selected < 3; selected++)
                    {
                        mode.SelectedIndex = selected;
                        settings.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                        check("GPU options match display mode " + selected,
                            gpu.Visibility == (selected == 1 ? Visibility.Visible : Visibility.Collapsed) &&
                            integrated.Visibility == (selected == 2 ? Visibility.Visible : Visibility.Collapsed));
                    }
                    var results = (System.Windows.Controls.ListBox)settings.FindName("SearchResults");
                    results.ItemsSource = Nepply.Services.SettingsSearch.Find("tần suất");
                    results.SelectedIndex = 0;
                    settings.ActivateSearchSelection();
                    check("Settings search reveals advanced monitor option", advanced.IsExpanded);
                }
            }
            finally { if (window is MainWindow main) main.Cleanup(); window.Close(); }
        }
    }
}
