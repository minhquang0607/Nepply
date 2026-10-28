using System.IO;
using System.Windows;
using System.Windows.Controls;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;

internal static class LocalizationTests
{
    internal static void Run(Action<string, bool> check, Action<int> pump, string output, Action<FrameworkElement, int, int, string> render)
    {
        foreach (var language in LocalizationService.Languages)
        {
            check($"Language {language.Id} displays native name", language.ToString() == language.Name);
            var baseline = new AppSettings { Language = "vi" };
            // Fixture preconditions must not inherit the runner's accessibility preferences.
            ThemeService.ApplyAccessibility(baseline, false, true);
            var settings = new SettingsWindow(baseline, _ => { }) { Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
            var light = (MaterialLightScope)((FrameworkElement)settings.FindName("SettingsContent")).GetValue(MetallicSurface.LightScopeProperty);
            try
            {
                settings.Show(); pump(25);
                if (language.Id == "vi")
                {
                    var section = (Border)settings.FindName("AppearanceSection");
                    check("Settings sections share moving light without metallic frames", light.Clock != null && !MetallicSurface.GetFrame(section)
                        && section.Effect == null && Equals(section.BorderBrush, Application.Current.Resources["BorderBrush"]));
                    check("Settings title no longer has a decorative material frame", !((Grid)settings.FindName("TitleArea")).Children.OfType<Border>().Any());
                    settings.Hide(); pump(20);
                    check("Hidden settings release material clock and pointer input", light.Clock == null && light.Pointer == null);
                    settings.Show(); pump(20);
                    check("Shown settings resume their material field", light.Clock != null);
                    ThemeService.ApplyAccessibility(baseline, false, false); pump(20);
                    check("Settings respect disabled system animations", light.Clock == null && light.Pointer == null);
                    ThemeService.ApplyAccessibility(baseline, false, true); pump(20);
                    check("Settings resume after system animations are enabled", light.Clock != null);
                }
                ((ComboBox)settings.FindName("LanguageChoice")).SelectedValue = language.Id;
                settings.FlushPreview(); pump(25); settings.UpdateLayout();
                check($"Language {language.Id} previews title without saving", ((TextBlock)settings.FindName("SettingsTitle")).Text == LocalizationService.Get("Settings") && baseline.Language == "vi");
                check($"Language {language.Id} exposes localized resource", Application.Current.Resources["Loc.Download"] is string s && !string.IsNullOrEmpty(s));
                check($"Language {language.Id} localizes the settings Apply and OK buttons",
                    Equals(((Button)settings.FindName("ApplyButton")).Content, LocalizationService.Get("Apply"))
                    && Equals(((Button)settings.FindName("OkButton")).Content, LocalizationService.Get("Ok")));
                if (language.Id == "vi")
                    check("Vietnamese settings use no English Apply or Monitor labels", LocalizationService.Get("Apply") == "Áp dụng"
                        && LocalizationService.Get("Monitor") == "Theo dõi"
                        && !LocalizationService.Get("DraftReady").Contains("Apply") && !LocalizationService.Get("SettingsExitGuidance").Contains("Apply")
                        && SettingsSearch.Find("monitor").All(e => e.Label.StartsWith("Theo dõi")) && SettingsSearch.Find("monitor").Length > 0);
                check($"Language {language.Id} search matches translated label", SettingsSearch.Find(LocalizationService.Get("Language")).Any(e => e.Control == "LanguageChoice"));
                render((FrameworkElement)settings.Content, 820, 740, Path.Combine(output, $"settings-language-{language.Id}.png"));
                Dialogs(check, pump, output, render, language.Id);
                ErrorLocalizationTests.Run(check, pump, settings, language.Id);
            }
            finally { settings.Close(); }
            check($"Language {language.Id} closed settings release shared light", light.Clock == null && light.Pointer == null);
            check($"Language {language.Id} cancel restores saved language", LocalizationService.CurrentLanguage == "vi");
        }
        PanelLocalizationTests.Run(check, pump);
        var invalid = new AppSettings { Language = "missing" }; invalid.Validate();
        check("Unknown language safely falls back to the Windows default", invalid.Language == LocalizationService.SystemDefault);
        check("Sheen is exactly five percent faster", Math.Abs(Nepply.Views.MetallicSurface.SweepDuration.TotalSeconds - 22 / 1.05) < .00001);
        ThemeService.Apply(new());
    }

    private static void Dialogs(Action<string, bool> check, Action<int> pump, string output, Action<FrameworkElement, int, int, string> render, string language)
    {
        var layout = new LayoutDecisionWindow { Left = -10000, Top = -10000, ShowActivated = false };
        var diagnostics = new SensorDiagnosticsWindow("Fixture report — no hardware access") { Width = 460, Height = 540, Left = -10000, Top = -10000, ShowActivated = false };
        var welcome = new WelcomeWindow(new AppSettings { Language = language, EdgePosition = EdgePosition.Bottom }) { Width = 460, Height = 650, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
        var monitor = new Nepply.ViewModels.HardwareMonitorViewModel();
        var source = new MonitorFixture();
        monitor.Initialize(source);
        try
        {
            layout.Show(); diagnostics.Show(); welcome.Show(); pump(20);
            check($"Language {language} translates dialog titles", layout.Title == LocalizationService.Get("LayoutDecisionTitle") && diagnostics.Title == LocalizationService.Get("Diagnostics"));
            var expected = new[] { "LayoutContinue", "LayoutDiscard", "LayoutCommit" }.Select(LocalizationService.Get).ToArray();
            check($"Language {language} translates every layout decision", Descendants(layout).OfType<Button>().Select(b => b.Content?.ToString()).SequenceEqual(expected));
            check($"Language {language} translates diagnostic copy action", Descendants(diagnostics).OfType<Button>().Any(b => Equals(b.Content, LocalizationService.Get("DiagnosticsCopy"))));
            check($"Language {language} translates initial monitor status", monitor.StatusText == LocalizationService.Get("MonitorConnecting") && monitor.SensorNote == LocalizationService.Get("TemperatureUnavailable"));
            var changed = new HashSet<string?>();
            monitor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            LocalizationService.Apply(language == "en" ? "ja" : "en");
            check($"Language {language} switch refreshes monitor text without sampling", new[] { "StatusText", "RamDetails", "SensorNote", "NetworkName" }.All(changed.Contains));
            LocalizationService.Apply(language); pump(10);
            check($"Language {language} updates onboarding without resetting selection", welcome.Title == LocalizationService.Get("WelcomeTitle")
                && ((Button)welcome.FindName("StartButton")).Content?.ToString() == LocalizationService.Get("WelcomeStart")
                && ((ComboBox)welcome.FindName("EdgeChoice")).SelectedIndex == (int)EdgePosition.Bottom
                && ((TextBlock)welcome.FindName("HotkeyHint")).Text == string.Format(LocalizationService.Get("WelcomeHotkey"), "Ctrl + Space"));
            check($"Language {language} preserves RAM numeric format placeholders", string.Format(LocalizationService.Get("RamInstalled"), 16d, 1.5d).Contains("GiB"));
            render((FrameworkElement)layout.Content, 460, 300, Path.Combine(output, $"layout-language-{language}.png"));
            render((FrameworkElement)diagnostics.Content, 460, 540, Path.Combine(output, $"diagnostics-language-{language}.png"));
            render((FrameworkElement)welcome.Content, 460, 650, Path.Combine(output, $"welcome-language-{language}.png"));
            check($"Language {language} narrow diagnostics keeps actions inside viewport", ActionsFit((FrameworkElement)diagnostics.Content));
            check($"Language {language} narrow welcome keeps actions inside viewport", ActionsFit((FrameworkElement)welcome.Content));
        }
        finally { monitor.Cleanup(); layout.Close(); diagnostics.Close(); welcome.Close(); }
    }

    private static bool ActionsFit(FrameworkElement root) => Descendants(root).OfType<Button>().Where(b => b.IsVisible).All(b =>
    {
        var bounds = b.TransformToAncestor(root).TransformBounds(new Rect(b.RenderSize));
        return bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= root.ActualWidth + 1 && bounds.Bottom <= root.ActualHeight + 1;
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class MonitorFixture : Nepply.Services.Telemetry.IHardwareMonitor
    {
        public event Action<HardwareInfoSnapshot>? HardwareUpdated { add { } remove { } }
        public HardwareInfoSnapshot GetSnapshot() => HardwareInfoSnapshot.Empty;
        public void Configure(AppSettings settings, bool visible) { }
        public void Start() { }
        public void Dispose() { }
    }
}
