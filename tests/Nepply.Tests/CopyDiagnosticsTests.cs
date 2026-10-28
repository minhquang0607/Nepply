using System.Windows;
using System.Windows.Controls;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;

internal static class CopyDiagnosticsTests
{
    internal static void Run(Action<string, bool> check, Action<int> pump)
    {
        LocalizationService.Apply("vi");
        var snapshot = HardwareInfoSnapshot.Empty with
        {
            Sequence = 3,
            Hardware = HardwareReading.Empty with { CpuName = "Fixture CPU", CpuTemp = 51 }
        };
        var settings = new SettingsWindow(new AppSettings(), _ => { }, null, snapshot);
        try
        {
            string? copied = null;
            settings.CopyText = text => copied = text;
            var button = (Button)settings.FindName("CopyDiagnosticsButton");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var info = (TextBlock)settings.FindName("InfoStatus");
            check("Copy diagnostics puts the shareable report on the clipboard",
                copied != null && copied.StartsWith(LocalizationService.Get("ReportTitle"))
                && copied.Contains("Windows: " + Environment.OSVersion.Version) && copied.Contains("Fixture CPU")
                && copied.Contains(LocalizationService.Get("ReportPrivacy")) && info.Text == LocalizationService.Get("DiagnosticsCopied"));
            check("The copied report holds no user profile path or account name",
                !copied!.Contains(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase)
                && !copied.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase));
            settings.CopyText = _ => throw new System.Runtime.InteropServices.COMException("Clipboard busy");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check("A busy clipboard reports a retryable error instead of crashing", info.Text == LocalizationService.Get("DiagnosticsClipboardBusy"));
        }
        finally { settings.Close(); }
    }
}
