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

            Uri? opened = null;
            copied = null;
            settings.CopyText = text => copied = text;
            settings.OpenLink = uri => opened = uri;
            ((Button)settings.FindName("ReportProblemButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check("Report a problem copies diagnostics and opens the prefilled bug form",
                copied != null && copied.Contains("Fixture CPU") && opened != null
                && opened.AbsoluteUri.StartsWith("https://github.com/minhquang0607/Nepply/issues/new?template=bug_report.yml&version=", StringComparison.Ordinal)
                && info.Text == LocalizationService.Get("ReportProblemOpened"));
            check("The bug form URL carries versions only, never diagnostics or account data",
                !opened!.AbsoluteUri.Contains("Fixture", StringComparison.OrdinalIgnoreCase)
                && !opened.AbsoluteUri.Contains(Uri.EscapeDataString(Environment.UserName), StringComparison.OrdinalIgnoreCase));
            check("Bug form names Windows 11 by build and keeps the exact build number",
                Uri.UnescapeDataString(SettingsWindow.BugReportUri("1.0.0-beta.1", new Version(10, 0, 26100)).Query)
                    == "?template=bug_report.yml&version=1.0.0-beta.1&windows=Windows 11 (build 26100)"
                && Uri.UnescapeDataString(SettingsWindow.BugReportUri("1.0.0", new Version(10, 0, 19045)).Query).EndsWith("windows=Windows 10 (build 19045)", StringComparison.Ordinal));
            settings.OpenLink = _ => throw new System.ComponentModel.Win32Exception(1155);
            ((Button)settings.FindName("ReportProblemButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check("Report a problem explains a missing browser instead of crashing", info.Text == LocalizationService.Get("OpenBrowserError"));
        }
        finally { settings.Close(); }
    }
}
