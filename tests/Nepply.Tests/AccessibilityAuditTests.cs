using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Nepply;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;

/// <summary>Walks the real visual tree, so a control added later cannot miss its screen reader name.</summary>
internal static class AccessibilityAuditTests
{
    private const int SettingsSections = 6;

    internal static void Run(Action<string, bool> check, Action<int> pump)
    {
        foreach (string language in new[] { "vi", "en" })
        {
            LocalizationService.Apply(language);
            var settings = new SettingsWindow(new AppSettings(), _ => { }) { Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                settings.Show();
                for (int section = 0; section < SettingsSections; section++)
                {
                    settings.ShowSettingsSection(section);
                    pump(100);
                    var unnamed = Unnamed(settings);
                    check($"Settings section {section} ({language}) names every control: {string.Join(", ", unnamed)}", unnamed.Length == 0);
                    var unreachable = Interactive(settings).Where(control => !Reachable(control)).Select(Describe).ToArray();
                    check($"Settings section {section} ({language}) reaches every control by keyboard: {string.Join(", ", unreachable)}", unreachable.Length == 0);
                }
            }
            finally { settings.Close(); }
            foreach (var window in new Window[] { new WelcomeWindow(new AppSettings()), new SensorDiagnosticsWindow("Nepply"), new LayoutDecisionWindow(), new MainWindow(preview: true) })
            {
                try
                {
                    window.Left = -10000; window.Top = -10000; window.ShowActivated = false; window.ShowInTaskbar = false;
                    window.Show();
                    if (window is MainWindow panel) panel.TogglePin();
                    pump(100);
                    int controls = Interactive(window).Count();
                    var unnamed = Unnamed(window);
                    check($"{window.GetType().Name} ({language}) names all {controls} controls: {string.Join(", ", unnamed)}", controls > 0 && unnamed.Length == 0);
                    var unreachable = Interactive(window).Where(control => !Reachable(control)).Select(Describe).ToArray();
                    check($"{window.GetType().Name} ({language}) reaches every control by keyboard: {string.Join(", ", unreachable)}", unreachable.Length == 0);
                }
                finally
                {
                    (window as MainWindow)?.Cleanup();
                    window.Close();
                }
            }
        }
        LocalizationService.Apply("vi");
    }

    private static string[] Unnamed(Visual root) => Interactive(root)
        .Where(control => string.IsNullOrWhiteSpace(UIElementAutomationPeer.CreatePeerForElement(control)?.GetName()))
        .Select(Describe).ToArray();

    private static IEnumerable<Control> Interactive(Visual root) => Descendants(root).OfType<Control>().Where(control =>
        control.TemplatedParent == null && control.IsVisible && control.IsEnabled
        && control is ButtonBase or ComboBox or Slider or TextBox or ListBox);

    // A ListBox takes the keyboard through its items, not through itself.
    private static bool Reachable(Control control) => control is ListBox list
        ? list.Items.Cast<object>().All(item => list.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem { Focusable: true })
        : control.IsTabStop;

    private static string Describe(Control control) => string.IsNullOrEmpty(control.Name) ? control.GetType().Name : control.Name;

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
