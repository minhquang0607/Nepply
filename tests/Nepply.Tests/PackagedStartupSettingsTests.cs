using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Nepply;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;
using Windows.ApplicationModel;

internal static class PackagedStartupSettingsTests
{
    internal static int Run(string output)
    {
        // This opt-in test must never modify another package's StartupTask.
        if (!ProcessPackageIdentity.IsPackaged || Package.Current.Id.Name != "Nepply.Internal.Preview") return 2;
        new AppSettings { HideDesktopIcons = false, AutoStart = false, HardwareSensorsEnabled = false, ShowMonitor = false }.Save();
        var app = new App(testHost: true) { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent();
        app.Startup += async (_, _) =>
        {
            StartupTask? task = null;
            bool claimed = false;
            MainWindow? panel = null;
            SettingsWindow? settings = null;
            var report = new Dictionary<string, object?>();
            int exit = 1;
            try
            {
                task = await StartupTask.GetAsync("NepplyStartup");
                report["Initial"] = task.State.ToString();
                if (task.State != StartupTaskState.Disabled) throw new InvalidOperationException("QA startup must initially be disabled.");
                claimed = true;
                // Do not Show the panel: Loaded also starts Desktop/hardware workflows.
                // Settings commit and MainWindow startup dispatch below are production code.
                panel = new MainWindow(preview: false, smokeTest: true);
                settings = new SettingsWindow(panel.Settings);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(MainWindow).GetField("_settingsWindow", flags)!.SetValue(panel, settings);
                var committed = typeof(MainWindow).GetMethod("SettingsApplied", flags)!;
                settings.SettingsCommitted += () => committed.Invoke(panel, null);
                settings.Show();
                var checkbox = (CheckBox)settings.FindName("AutoStart");
                var apply = (Button)settings.FindName("ApplyButton");
                foreach (bool desired in new[] { true, false })
                {
                    checkbox.IsChecked = desired;
                    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var deadline = DateTime.UtcNow.AddSeconds(15);
                    while ((task.State == StartupTaskState.Enabled) != desired && DateTime.UtcNow < deadline) await Task.Delay(100);
                    // Allow the production continuation to synchronize persisted settings.
                    await app.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    bool actual = task.State == StartupTaskState.Enabled;
                    if (actual != desired || panel.Settings.AutoStart != desired || AppSettings.Load().AutoStart != desired || checkbox.IsChecked != desired)
                        throw new InvalidOperationException("OS/UI/persisted startup state mismatch: " + task.State);
                    report[desired ? "AfterApplyEnable" : "AfterApplyDisable"] = task.State.ToString();
                    if (!settings.IsVisible) throw new InvalidOperationException("Apply unexpectedly closed Settings.");
                }
                report["Passed"] = true;
                exit = 0;
            }
            catch (Exception ex) { report["Error"] = ex.ToString(); report["Passed"] = false; }
            finally
            {
                try
                {
                    if (claimed && task != null) task.Disable();
                    report["CleanupState"] = task?.State.ToString();
                    settings?.Close();
                    panel?.Cleanup();
                    panel?.Close();
                }
                catch (Exception ex) { report["CleanupError"] = ex.ToString(); report["Passed"] = false; exit = 1; }
                report["Scope"] = "Production Settings Apply -> MainWindow SettingsApplied -> StartupCoordinator -> packaged backend -> OS and isolated persisted settings. Panel Loaded/hardware/Desktop not started; not logon or full production-process lifecycle acceptance.";
                File.WriteAllText(Path.Combine(output, "startup-settings-result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                app.Shutdown(exit);
            }
        };
        return app.Run();
    }
}
