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
        Loaded+=(_,_)=>Helpers.DialogPlacement.FitInitial(this);
        EdgeChoice.SelectedIndex = (int)settings.EdgePosition;
        HideIcons.IsChecked = false;
        RefreshLanguage((int)settings.EdgePosition);
        LocalizationService.Changed+=LanguageChanged;
        Closed+=(_,_)=>LocalizationService.Changed-=LanguageChanged;
    }
    private void LanguageChanged()=>RefreshLanguage(EdgeChoice.SelectedIndex);
    private void RefreshLanguage(int selected)
    {
        EdgeChoice.ItemsSource=Enum.GetNames<EdgePosition>().Select(LocalizationService.Get).ToArray();
        EdgeChoice.SelectedIndex=selected;
        HotkeyHint.Text=string.Format(LocalizationService.Get("WelcomeHotkey"),MainWindow.HotkeyLabel(_settings));
        ShortcutList.ItemsSource=ShortcutLines(_settings);
    }
    /// <summary>"Action · Key" for every assigned shortcut except show/hide, which has its own line.</summary>
    internal static string[] ShortcutLines(AppSettings settings) => ShortcutBindings.All
        .Where(action=>action!=ShortcutAction.TogglePanel)
        .Select(action=>(Action:action,Gesture:ShortcutBindings.Get(settings,action)))
        .Where(item=>!item.Gesture.IsEmpty)
        .Select(item=>LocalizationService.Get(SettingsWindow.ShortcutLabelKey(item.Action))+" · "+item.Gesture)
        .ToArray();
    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (IsGuide) { Close(); return; }
        try
        {
            OnboardingService.Complete(_settings, (EdgePosition)EdgeChoice.SelectedIndex,
                HideIcons.IsChecked == true, draft => draft.Save());
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex,"Complete onboarding");
            ErrorText.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty,"Loc.WelcomeSaveError");
        }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void DragTitle(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
