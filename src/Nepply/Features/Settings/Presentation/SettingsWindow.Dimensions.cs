using System.Windows.Controls;
using Nepply.Helpers;
using Nepply.Modules.Panel;

namespace Nepply.Views;

public partial class SettingsWindow
{
    private void LoadRelativeDimensions()
    {
        ConfigureRelativeDimensions();
        var screen = ScreenManager.SelectAvailable(ScreenManager.GetScreens(), _draft.MonitorDevice);
        if(screen == null) return;
        var available = PanelDimensions.Available(screen);
        var resolved = PanelDimensions.Resolve(_draft, screen);
        WidthSlider.Value = resolved.Side / available.Width * 100;
        TopWidthSlider.Value = resolved.Top / available.Width * 100;
        HeightSlider.Value = resolved.Height / available.Height * 100;
    }

    private void ConfigureRelativeDimensions()
    {
        var screen = ScreenManager.SelectAvailable(ScreenManager.GetScreens(), ScreenChoice.SelectedValue as string ?? "");
        if(screen == null) return;
        var available = PanelDimensions.Available(screen);
        Configure(WidthSlider, 320, available.Width);
        Configure(TopWidthSlider, 720, available.Width);
        Configure(HeightSlider, 240, available.Height);
    }

    private static void Configure(Slider slider, double minimum, double available)
    {
        slider.Minimum = Math.Min(100, minimum / available * 100);
        slider.Maximum = 100;
        slider.TickFrequency = 1;
        slider.SetResourceReference(System.Windows.FrameworkElement.ToolTipProperty,"Loc.DimensionsHint");
    }
}
