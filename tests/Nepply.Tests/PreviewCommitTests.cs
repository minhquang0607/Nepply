using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Nepply.Models;
using Nepply.Views;

internal static class PreviewCommitTests
{
    internal static void Run(Action<string, bool> check)
    {
        var appliedTarget = new AppSettings { NetworkInterfaceId = "missing-adapter-fixture" }; int applyWrites = 0, commits = 0;
        var appliedWindow = new SettingsWindow(appliedTarget, _ => applyWrites++) { Left = -10000, Top = -10000, ShowInTaskbar = false };
        appliedWindow.SettingsCommitted += () => commits++;
        appliedWindow.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                ((Slider)appliedWindow.FindName("WidthSlider")).Value = 60;
                typeof(SettingsWindow).GetMethod("Apply_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(appliedWindow, new object[] { appliedWindow, new RoutedEventArgs() });
                check("Apply persists and notifies without closing", appliedWindow.IsVisible && applyWrites == 1 && commits == 1 && appliedTarget.PanelWidthPercent == 60);
                check("Unrelated Apply preserves an unavailable network adapter", appliedTarget.NetworkInterfaceId == "missing-adapter-fixture");
                ((Slider)appliedWindow.FindName("WidthSlider")).Value = 70;
                appliedWindow.FlushPreview();
                appliedWindow.Close();
                check("Cancel after Apply retains last committed settings", appliedTarget.PanelWidthPercent == 60 && applyWrites == 1);
            }
            catch (Exception ex) { check("Apply fixture: " + ex, false); appliedWindow.Close(); }
        }));
        appliedWindow.ShowDialog();
        var updateTarget = new AppSettings(); int updateWrites = 0;
        var updateWindow = new SettingsWindow(updateTarget, _ => updateWrites++) { Left = -10000, Top = -10000, ShowInTaskbar = false };
        updateWindow.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var autoCheck = (CheckBox)updateWindow.FindName("AutoCheckUpdates");
                check("Daily update check starts unticked and visible", autoCheck.IsChecked == false && autoCheck.Visibility == Visibility.Visible);
                autoCheck.IsChecked = true;
                typeof(SettingsWindow).GetMethod("Apply_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(updateWindow, new object[] { updateWindow, new RoutedEventArgs() });
                check("Apply saves the daily update check", updateTarget.AutoCheckUpdates && updateWrites == 1);
                check("Settings search finds the daily update check", Nepply.Services.SettingsSearch.Find("cập nhật").Any(entry => entry.Control == "AutoCheckUpdates"));
            }
            catch (Exception ex) { check("Daily update fixture: " + ex, false); }
            updateWindow.Close();
        }));
        updateWindow.ShowDialog();
        foreach (bool fail in new[] { true, false })
        {
            var target = new AppSettings(); int writes = 0;
            var window = new SettingsWindow(target, candidate =>
            { writes++; if (fail) throw new System.IO.IOException("fixture failure"); })
            { Left = -10000, Top = -10000, ShowInTaskbar = false };
            window.PreviewViewport = () => new Size(360, 500);
            window.PreviewChanged += _ => { };
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var picker = (DotGridPicker)window.FindName("GridPicker");
                    picker.Columns = 12; picker.Rows = 10;
                    var save = typeof(SettingsWindow).GetMethod("Save_Click", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    ((Slider)window.FindName("WidthSlider")).Value = 60;
                    save.Invoke(window, new object[] { window, new RoutedEventArgs() });
                    if (fail)
                    {
                        check("Failed save retains original settings and open draft", target.PanelWidth == 380 && window.IsVisible && ((TextBlock)window.FindName("ErrorText")).Text.Contains("fixture failure"));
                        window.Close();
                    }
                }
                catch (Exception ex) { check("Modal preview commit: " + ex, false); window.Close(); }
            }));
            bool? saved = window.ShowDialog();
            check($"Modal save outcome retains adaptive grid intent ({fail})", writes == 1 && (fail ? saved != true && target.PanelWidthPercent == 0 && target.GridColumns == 4 : saved == true && target.PanelWidthPercent == 60 && target.GridColumns == 5 && target.GridRows == 10));
        }
    }
}
