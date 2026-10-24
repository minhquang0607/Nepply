using System.Windows;
using System.Windows.Controls;
using Nepply.Services;
using Nepply.Views;

/// <summary>A missing temperature is shown as a muted dash that explains itself on hover.</summary>
internal static class TemperatureDashTests
{
    internal static void Run(Action<string,bool> check, Action<int> pump)
    {
        LocalizationService.Apply("vi");
        var reading = new MetricReadingView { Label = "CPU", ValueText = "24%", SecondaryText = "48°C" };
        var host = new Window { Content = reading, Width = 200, Height = 140, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            host.Show(); pump(10);
            var secondary = (TextBlock)reading.FindName("SecondaryValue");
            check("A temperature reading uses the accent color and names the metric",
                Equals(secondary.Foreground, Application.Current.Resources["AccentBrush"]) && Equals(secondary.ToolTip, LocalizationService.Get("Temperature")));
            reading.SecondaryText = "—"; pump(10);
            check("A missing temperature is a muted dash explaining why on hover",
                Equals(secondary.Foreground, Application.Current.Resources["MutedBrush"])
                && Equals(secondary.ToolTip, LocalizationService.Get("TemperatureUnavailable")) && secondary.Visibility == Visibility.Visible);
            LocalizationService.Apply("en"); pump(10);
            check("The explanation follows the language", Equals(secondary.ToolTip, LocalizationService.Get("TemperatureUnavailable")));
            reading.SecondaryText = ""; pump(10);
            check("Cards without a temperature show nothing", secondary.Visibility == Visibility.Collapsed);
        }
        finally { host.Close(); LocalizationService.Apply("vi"); }
    }
}
