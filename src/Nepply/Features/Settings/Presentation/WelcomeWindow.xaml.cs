using System.Windows;
using System.Windows.Input;
using Nepply.Models;
using Nepply.Services;

namespace Nepply.Views;

public partial class WelcomeWindow : Window
{
    private readonly AppSettings _settings;
    internal bool IsGuide { get; }
    /// <param name="guideOnly">Reopened from Settings or the tray: show the guide without first-run choices.</param>
    public WelcomeWindow(AppSettings settings, bool guideOnly = false)
    {
        _settings = settings;
        IsGuide = guideOnly;
        InitializeComponent();
        if (guideOnly)
        {
            SetupArea.Visibility = Visibility.Collapsed;
            StartButton.SetResourceReference(ContentProperty, "Loc.Close");
        }
        Loaded += (_, _) => Helpers.DialogPlacement.FitInitial(this);
        EdgeChoice.SelectedIndex = (int)settings.EdgePosition;
        HideIcons.IsChecked = false;
        LanguageChoice.ItemsSource = LocalizationService.Languages;
        LanguageChoice.SelectedValue = settings.Language;
        _languageReady = true;
        RefreshLanguage((int)settings.EdgePosition);
        LocalizationService.Changed += LanguageChanged;
        Closed += (_, _) =>
        {
            LocalizationService.Changed -= LanguageChanged;
            // Only a preview from this window needs undoing; a completed start already saved it.
            if (_previewedLanguage) LocalizationService.Apply(_settings.Language);
        };
    }
    private bool _languageReady, _previewedLanguage;
    internal string SelectedLanguage => LanguageChoice.SelectedValue as string ?? _settings.Language;
    // The first choice is shown live so the rest of the screen is readable before starting.
    private void LanguageChoice_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_languageReady || LanguageChoice.SelectedValue is not string language || language == LocalizationService.CurrentLanguage) return;
        _previewedLanguage = true;
        LocalizationService.Apply(language);
    }
    private void LanguageChanged() => RefreshLanguage(EdgeChoice.SelectedIndex);
    private void RefreshLanguage(int selected)
    {
        EdgeChoice.ItemsSource = Enum.GetNames<EdgePosition>().Select(LocalizationService.Get).ToArray();
        EdgeChoice.SelectedIndex = selected;
        HotkeyHint.Text = string.Format(LocalizationService.Get("WelcomeHotkey"), MainWindow.HotkeyLabel(_settings));
        ShortcutList.ItemsSource = ShortcutLines(_settings);
    }
    /// <summary>"Action · Key" for every assigned shortcut except show/hide, which has its own line.</summary>
    internal static string[] ShortcutLines(AppSettings settings) => ShortcutBindings.All
        .Where(action => action != ShortcutAction.TogglePanel)
        .Select(action => (Action: action, Gesture: ShortcutBindings.Get(settings, action)))
        .Where(item => !item.Gesture.IsEmpty)
        .Select(item => LocalizationService.Get(SettingsWindow.ShortcutLabelKey(item.Action)) + " · " + item.Gesture)
        .ToArray();
    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (IsGuide) { Close(); return; }
        try
        {
            OnboardingService.Complete(_settings, (EdgePosition)EdgeChoice.SelectedIndex,
                HideIcons.IsChecked == true, draft => draft.Save(), SelectedLanguage);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Complete onboarding");
            ErrorText.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, "Loc.WelcomeSaveError");
        }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void DragTitle(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
