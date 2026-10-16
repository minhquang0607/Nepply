using System.Windows;
using System.Windows.Input;
using Nepply.Models;
using Nepply.Services;

namespace Nepply.Views;

public partial class WelcomeWindow : Window
{
    private readonly AppSettings _settings;
    public WelcomeWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
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
    }
    private void Start_Click(object sender, RoutedEventArgs e)
    {
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
