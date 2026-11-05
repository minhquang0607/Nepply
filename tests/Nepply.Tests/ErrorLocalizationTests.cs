using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Nepply;
using Nepply.Models;
using Nepply.Modules.Desktop;
using Nepply.Services;
using Nepply.ViewModels;
using Nepply.Views;

internal static class ErrorLocalizationTests
{
    internal static void Run(Action<string, bool> check, Action<int> pump, SettingsWindow settings, string language)
    {
        var catalog = new Catalog();
        var panel = new IconPanel();
        int subscribers = LanguageSubscribers();
        panel.Initialize(catalog, new Actions(), new AppSettings());
        panel.Initialize(catalog, new Actions(), new AppSettings());
        pump(20);
        try
        {
            foreach (string key in new[] { "FatalUiError", "StartupError", "OpenLogsError" })
                check($"Language {language} application error {key} uses current language",
                    App.ErrorMessage(key, "fallback") == LocalizationService.Get(key) && LocalizationService.Get(key) != key);
            check("Application error reporting survives language initialization failure",
                App.ErrorMessage("StartupError", "fallback", _ => throw new TypeInitializationException("LocalizationService", new FormatException())) == "fallback");
            check("Application error reporting falls back for missing or empty resources",
                App.ErrorMessage("missing", "fallback", key => key) == "fallback"
                && App.ErrorMessage("missing", "fallback", _ => " ") == "fallback");
            using (var tray = new Hardcodet.Wpf.TaskbarNotification.TaskbarIcon())
            {
                var detach = App.AttachTrayLanguage(tray);
                try
                {
                    check($"Language {language} tray tooltip uses resource", tray.ToolTipText == LocalizationService.Get("TrayTooltip"));
                    LocalizationService.Apply(language == "en" ? "ja" : "en");
                    check($"Language {language} existing tray tooltip follows language preview", tray.ToolTipText == LocalizationService.Get("TrayTooltip"));
                    LocalizationService.Apply(language);
                }
                finally { detach(); }
            }
            var vm = (DesktopIconViewModel)panel.DataContext;
            var searchBox = (TextBox)settings.FindName("SearchSettings");
            var searchResults = (ListBox)settings.FindName("SearchResults");
            searchBox.Text = "language";
            searchResults.SelectedItem = searchResults.Items.Cast<SettingsSearch.Entry>().Single(entry => entry.Control == "LanguageChoice");
            LocalizationService.Apply(language == "en" ? "ja" : "en");
            check($"Language {language} existing settings search refreshes wording and retains selection",
                searchResults.SelectedItem is SettingsSearch.Entry selected && selected.Control == "LanguageChoice"
                && selected.Label == LocalizationService.Get("Language") && searchBox.Text == "language");
            LocalizationService.Apply(language); searchBox.Clear();
            check($"Language {language} preset tooltip descriptions are translated",
                ((ItemsControl)settings.FindName("PresetCards")).Items.Cast<LayoutPreset>().All(p => p.Description == LocalizationService.Get(
                    p.Id switch { "compact" => "CompactPresetDescription", "spacious" => "SpaciousPresetDescription", _ => "RibbonPresetDescription" })));
            check($"Language {language} guide tips have no XML character reference", !LocalizationService.Get("WelcomeTips").Contains("&#10;"));
            Exception Thrown(Action action) { try { action(); } catch (Exception ex) { return ex; } throw new InvalidOperationException("Fixture did not throw."); }
            foreach (var (key, thrown) in new (string, Exception)[]{
                ("LayoutVersionError",Thrown(()=>(LayoutProfile.Capture(new AppSettings()) with {Version=2}).ApplyTo(new AppSettings()))),
                ("LayoutTooLargeError",Thrown(()=>LayoutProfile.Parse(new string(' ',65537)))),
                ("LayoutFormatError",Thrown(()=>LayoutProfile.Parse("{}"))),
                ("LayoutEmptyError",new System.Text.Json.JsonException("Empty layout file.").WithUserMessage("LayoutEmptyError")),
                ("ProtectedSettingsError",new System.IO.IOException("Protected settings cannot be overwritten.").WithUserMessage("ProtectedSettingsError")),
                ("SettingsTooLargeError",new System.IO.InvalidDataException("Settings exceed the 2 MiB limit.").WithUserMessage("SettingsTooLargeError"))})
                check($"Language {language} renders operation error {key} from its key",
                    LocalizationService.Render(UserMessage.From(thrown)) == LocalizationService.Get(key) && LocalizationService.Get(key) != key && !thrown.Message.Contains(LocalizationService.Get(key)));
            const string unknownError = "OS detail {0}: C:\\日本語\\file.txt";
            check($"Language {language} preserves unknown OS exception detail", LocalizationService.Render(UserMessage.From(new System.IO.IOException(unknownError))) == unknownError);
            const string vietnameseDetail = "Chưa thể ẩn icon Desktop. Hãy thử lại khi Windows Explorer sẵn sàng.";
            check($"Language {language} verbatim text is never looked up as a translation", LocalizationService.Render(UserMessage.FromText(vietnameseDetail)) == vietnameseDetail);
            var displayChoices = SettingsWindow.BuildScreenChoices([new Nepply.Helpers.DisplayScreen("DISPLAY1", 0, 0, 1920, 1080, 1, true)], "missing {0} 日本語");
            var missingChoice = displayChoices.Single(choice => choice.Id == "missing {0} 日本語");
            check($"Language {language} disconnected display preserves identity and translates label",
                missingChoice.Name == string.Format(LocalizationService.Get("DisconnectedDevice"), missingChoice.Id));
            LocalizationService.Apply(language == "en" ? "ja" : "en");
            var refreshedChoice = missingChoice.Localized();
            check($"Language {language} device label refresh retains exact saved ID",
                refreshedChoice.Id == missingChoice.Id && refreshedChoice.Name == string.Format(LocalizationService.Get("DisconnectedDevice"), missingChoice.Id)
                && displayChoices.Single(choice => choice.Id == "DISPLAY1").Localized().Name == string.Format(LocalizationService.Get("PrimaryDisplayLabel"), "DISPLAY1"));
            LocalizationService.Apply(language);
            foreach (var (control, key) in new[]{("GlowSlider","Glow"),("DwellSlider","TriggerDelay"),("TopWidthSlider","TopWidth"),
                ("HotkeyButton","Hotkey"),("SettingsNavigation","SettingsSections"),("ColorPicker","ColorFieldHelp"),
                ("SearchResults","SettingsSearchResults")})
                check($"Language {language} accessibility name of {control} uses its resource",
                    System.Windows.Automation.AutomationProperties.GetName((DependencyObject)settings.FindName(control)) == LocalizationService.Get(key));
            check($"Language {language} Desktop list accessibility is localized",
                System.Windows.Automation.AutomationProperties.GetName((DependencyObject)panel.FindName("IconList")) == LocalizationService.Get("DesktopItems"));
            var sourceReading = HardwareReading.Empty with
            {
                CpuSourceInfo = new("sensor {0} 日本語", "AMD ADL PMLog", false, true),
                Gpus = [new("private-id", "GPU {1}", 20, 40, 1000, "legacy source") { SourceInfo = new("GpuAmd", true, true) }]
            };
            var roundTrip = System.Text.Json.JsonSerializer.Deserialize<HardwareReading>(System.Text.Json.JsonSerializer.Serialize(sourceReading))!;
            check($"Language {language} sensor metadata survives JSON without changing identity",
                roundTrip.CpuSourceInfo == sourceReading.CpuSourceInfo && roundTrip.Gpus[0].SourceInfo == sourceReading.Gpus[0].SourceInfo
                && roundTrip.Gpus[0].Id == "private-id");
            check($"Language {language} CPU source labels preserve sensor text",
                TelemetrySourceText.Cpu(roundTrip) == string.Format(LocalizationService.Get("SourceCpu"), "sensor {0} 日本語",
                    string.Format(LocalizationService.Get("SourceFallback"), "AMD ADL PMLog")));
            var gpuCard = new GpuCardViewModel(roundTrip.Gpus[0]);
            check($"Language {language} GPU source labels preserve APIs and device name",
                gpuCard.Details.Contains("GPU {1}") && gpuCard.Details.Contains("AMD ADL PMLog EDGE")
                && gpuCard.Details.Contains(LocalizationService.Get("SourceD3dLoad")));
            var unavailable = Nepply.Services.Telemetry.TelemetryFreshness.Unavailable(roundTrip, "SensorExpired");
            check($"Language {language} expired sensor metadata is cleared with its measurements",
                unavailable.CpuSourceInfo == null && unavailable.Gpus[0].SourceInfo == null
                && !TelemetrySourceText.Cpu(unavailable).Contains("sensor {0}") && unavailable.Gpus[0].Temperature == null);
            check($"Language {language} expired sensor source shows the localized status",
                TelemetrySourceText.Cpu(unavailable) == LocalizationService.Get("SensorExpired")
                && TelemetrySourceText.Gpu(unavailable.Gpus[0]) == LocalizationService.Get("ReportUnavailable"));
            var provided = HardwareReading.Empty with { CpuTelemetrySource = "Temperature: LibreHardwareMonitor · Core (Tctl/Tdie)" };
            check($"Language {language} language-neutral provider source is shown unchanged",
                TelemetrySourceText.Cpu(provided) == provided.CpuTelemetrySource);
            var monitorSource = new FixedMonitor(HardwareInfoSnapshot.Empty with { Sequence = 1, NetworkName = "", NetworkStatus = "NetworkReadError" });
            var monitor = new HardwareMonitorViewModel();
            try
            {
                // Frames are applied at Background priority; pump until delivered, bounded.
                bool PumpUntil(Func<bool> done) { for (int i = 0; i < 100 && !done(); i++) pump(2); return done(); }
                monitor.Initialize(monitorSource); monitor.SetActive(true);
                check($"Language {language} network status renders from its key", PumpUntil(() => monitor.NetworkName == LocalizationService.Get("NetworkReadError")));
                monitorSource.Publish(HardwareInfoSnapshot.Empty with { Sequence = 2, NetworkName = "Wi-Fi {0} 日本語", NetworkStatus = null });
                check($"Language {language} adapter name is shown verbatim", PumpUntil(() => monitor.NetworkName == "Wi-Fi {0} 日本語"));
            }
            finally { monitor.Cleanup(); }
            var groupedIcon = new DesktopIcon { Path = "C:\\日本語\\{0}.lnk", SourcePaths = ["C:\\日本語\\{0}.lnk", "C:\\Public\\{1}.lnk"] };
            vm.Icons.Add(groupedIcon);
            int tooltipChanges = 0;
            groupedIcon.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(DesktopIcon.SourceDescription)) tooltipChanges++; };
            var converter = new Nepply.Converters.UserMessageConverter();
            string Tooltip() => (string)converter.Convert(groupedIcon.SourceDescription, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture);
            check($"Language {language} grouped tooltip preserves Unicode and literal braces",
                Tooltip() == string.Format(LocalizationService.Get("GroupedShortcutDescription"), 2, groupedIcon.Path, string.Join("\n", groupedIcon.SourcePaths)));
            LocalizationService.Apply(language == "en" ? "ja" : "en");
            check($"Language {language} existing grouped tooltip is invalidated without scan", tooltipChanges > 0
                && Tooltip().Contains(groupedIcon.Path) && Tooltip().Contains(groupedIcon.SourcePaths[1]));
            LocalizationService.Apply(language);
            vm.Icons.Remove(groupedIcon);
            // Invalid allowlisted-document name cannot launch a process or touch user files.
            typeof(SettingsWindow).GetMethod("OpenLocalDocument_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [new Button { Tag = "not-allowed.md" }, new RoutedEventArgs()]);
            var info = (TextBlock)settings.FindName("InfoStatus");
            check($"Language {language} missing local document uses resource", info.Text == LocalizationService.Get("LocalDocumentMissing"));
            typeof(SettingsWindow).GetMethod("Hotkey_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(settings, [settings, new RoutedEventArgs()]);
            var hotkeyButton = (Button)settings.FindName("HotkeyButton");
            LocalizationService.Apply(language == "en" ? "ja" : "en");
            check($"Language {language} document notice and recording prompt update in place",
                info.Text == LocalizationService.Get("LocalDocumentMissing")
                && Equals(hotkeyButton.Content, LocalizationService.Get("RecordHotkeyPrompt")));
            typeof(SettingsWindow).GetMethod("LoadDraft", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(settings, null);
            settings.FlushPreview(); // Consume LoadDraft's queued preview before testing an independent language change.
            check($"Language {language} loading draft cancels hidden hotkey recording",
                !(bool)typeof(SettingsWindow).GetField("_recording", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(settings)!
                && !Equals(hotkeyButton.Content, LocalizationService.Get("RecordHotkeyPrompt")));
            LocalizationService.Apply(language);
            var text = (TextBlock)panel.FindName("LaunchNoticeText");
            var notice = (Border)panel.FindName("LaunchNotice");
            var error = (TextBlock)settings.FindName("ErrorText");
            string filename = "tệp {0} 日本語.exe";
            object?[] arguments = [filename, UserMessage.FromKey("DesktopLaunchMissing")];
            var message = UserMessage.FromKey("DesktopLaunchFailed", arguments);
            arguments[0] = "mutated caller array";
            panel.ShowActionFailure(message);
            check($"Language {language} renders nested action failure without interpreting filename braces",
                text.Text == string.Format(LocalizationService.Get("DesktopLaunchFailed"), filename, LocalizationService.Get("DesktopLaunchMissing")));
            check($"Language {language} localizes scan error at presentation boundary", vm.ScanError == LocalizationService.Get("DesktopReadError"));
            var stateText = vm.ScanError;
            string changed = language == "en" ? "ja" : "en";
            Console.WriteLine($"TRACE {language}: apply notice language {changed}");
            LocalizationService.Apply(changed);
            Console.WriteLine($"TRACE {language}: pump notice language");
            pump(10);
            Console.WriteLine($"TRACE {language}: notice language pump returned");
            check($"Language {language} open notice and scan error update without new scan",
                notice.Visibility == Visibility.Visible && text.Text.Contains(filename)
                && text.Text.Contains(LocalizationService.Get("DesktopLaunchMissing"))
                && vm.ScanError == LocalizationService.Get("DesktopReadError") && vm.ScanError != stateText);
            panel.ShowActionNotice("DesktopHideError");
            LocalizationService.Apply(language); pump(10);
            check($"Language {language} translates fixed service notice", text.Text == LocalizationService.Get("DesktopHideError"));
            panel.DismissActionNotice("DesktopRestoreError");
            check($"Language {language} unrelated dismissal keeps notice", notice.Visibility == Visibility.Visible);
            panel.DismissActionNotice("DesktopHideError");
            LocalizationService.Apply(changed);
            check($"Language {language} dismissal uses original identity and stays dismissed", notice.Visibility == Visibility.Collapsed && text.Text == "");

            var notices = new List<UserMessage> {
                UserMessage.FromKey("AutoStartUpdateError"),
                UserMessage.FromKey("DesktopHideError") };
            var combined = UserMessage.Join(notices, Environment.NewLine);
            notices.Clear();
            panel.ShowActionFailure(combined);
            LocalizationService.Apply(language);
            check($"Language {language} translates each combined shell notice and snapshots its list",
                text.Text == LocalizationService.Get("AutoStartUpdateError") + Environment.NewLine + LocalizationService.Get("DesktopHideError"));
            LocalizationService.Apply(changed);
            check($"Language {language} existing combined shell notice changes language without another OS action",
                text.Text == LocalizationService.Get("AutoStartUpdateError") + Environment.NewLine + LocalizationService.Get("DesktopHideError"));
            panel.DismissActionFailure(combined);
            LocalizationService.Apply(language);
            check($"Language {language} dismissed combined notice stays dismissed", notice.Visibility == Visibility.Collapsed);
            check($"Language {language} empty message group is empty", LocalizationService.Render(UserMessage.Join([])) == "");
            foreach (string key in new[] { "AutoStartDifferentEntry", "AutoStartSeparateShortcut", "AutoStartOffInWindows" })
                check($"Language {language} startup notice {key} has a translated resource",
                    LocalizationService.Render(UserMessage.FromKey(key)) == LocalizationService.Get(key)
                    && LocalizationService.Get(key) != key);

            LocalizationService.Apply(language);
            ((ComboBox)settings.FindName("AccentChoice")).SelectedItem = "Custom";
            foreach (string key in new[] { "RecoveryNewer", "RecoveryDamaged", "RecoveryRestored", "RecoveryBackupNewer", "RecoveryUnreadable" })
            {
                var recovery = UserMessage.FromKey(key);
                string translated = LocalizationService.Render(recovery);
                LocalizationService.Apply(language == "vi" ? "en" : "vi");
                check($"Language {language} recovery {key} translates without reading settings again",
                    translated != key && translated != LocalizationService.Render(recovery));
                LocalizationService.Apply(language);
            }
            ((TextBox)settings.FindName("CustomAccent")).Text = "invalid";
            typeof(SettingsWindow).GetMethod("Apply_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(settings, [settings, new RoutedEventArgs()]);
            check($"Language {language} actual invalid-color Apply shows localized error", error.Text == LocalizationService.Get("ValidateHex"));
            ((ComboBox)settings.FindName("LanguageChoice")).SelectedValue = changed;
            settings.FlushPreview(); pump(10);
            check($"Language {language} invalid field does not block language preview", LocalizationService.CurrentLanguage == changed && error.Text == LocalizationService.Get("ValidateHex"));
            settings.ShowExitGuidance();
            check($"Language {language} exit guidance names current save actions", error.Text == LocalizationService.Get("SettingsExitGuidance"));
            ((ComboBox)settings.FindName("LanguageChoice")).SelectedValue = language;
            settings.FlushPreview(); pump(10);
            check($"Language {language} existing guidance changes language", error.Text == LocalizationService.Get("SettingsExitGuidance"));
            ((TextBox)settings.FindName("CustomAccent")).Text = "#39E7F5";
            ((ComboBox)settings.FindName("AccentChoice")).SelectedItem = "Champagne";
            settings.FlushPreview();
            DiagnosticReport(check, pump, language);
        }
        finally
        {
            panel.Cleanup();
            LocalizationService.Apply(language);
        }
        check($"Language {language} repeated initialization and cleanup balance static subscriptions", LanguageSubscribers() == subscribers);
        LocalizationService.Apply(language == "en" ? "ja" : "en");
        check($"Language {language} disposed panel cannot resurrect a stale notice", ((Border)panel.FindName("LaunchNotice")).Visibility == Visibility.Collapsed);
        LocalizationService.Apply(language);
    }

    private static void DiagnosticReport(Action<string, bool> check, Action<int> pump, string language)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        var sample = HardwareInfoSnapshot.Empty with
        {
            Sequence = 1,
            MonotonicTimestamp = now,
            NetworkName = "PRIVATE_NETWORK",
            Hardware = HardwareReading.Empty with
            {
                CpuName = "CPU {0} 日本語",
                Error = "PRIVATE_ERROR",
                MonotonicTimestamp = now,
                Gpus = [new("PRIVATE_GPU_ID", "GPU {1}", 25, null, 1000, "API source")]
            }
        };
        string Build() => Nepply.Services.Telemetry.SensorDiagnosticReport.Build(sample, "PrivilegeUser", "DriverAbsent", now);
        int subscribers = LanguageSubscribers();
        var dialog = new SensorDiagnosticsWindow(Build) { Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            dialog.Show(); pump(10);
            var text = (TextBox)dialog.FindName("ReportText");
            check($"Language {language} production diagnostic body uses translated labels and states",
                text.Text.Contains(LocalizationService.Get("ReportTitle"))
                && text.Text.Contains(LocalizationService.Get("PrivilegeUser"))
                && text.Text.Contains(LocalizationService.Get("DriverAbsent"))
                && text.Text.Contains(LocalizationService.Get("ReportUnavailable"))
                && text.Text.Contains(LocalizationService.Get("ReportPrivacy")));
            check($"Language {language} diagnostic still excludes sensitive fields", !text.Text.Contains("PRIVATE_")
                && text.Text.Contains("CPU {0} 日本語") && text.Text.Contains("GPU {1}") && text.Text.Contains("API source"));
            LocalizationService.Apply(language == "en" ? "ja" : "en");
            check($"Language {language} open diagnostic changes language without resampling",
                text.Text.Contains(LocalizationService.Get("ReportCapture"))
                && text.Text.Contains(string.Format(LocalizationService.Get("ReportSensorAge"), 0d)));
        }
        finally { dialog.Close(); LocalizationService.Apply(language); }
        check($"Language {language} diagnostic closes without static subscription leak", LanguageSubscribers() == subscribers);
    }

    private static int LanguageSubscribers() => (typeof(LocalizationService).GetField("Changed", BindingFlags.Static | BindingFlags.NonPublic)!
        .GetValue(null) as Delegate)?.GetInvocationList().Length ?? 0;
    private sealed class Catalog : IDesktopCatalog
    {
        public IReadOnlyList<DesktopIcon> GetIcons() => [];
        public DesktopScanState ScanState => new(false, false, "DesktopReadError");
        public event Action? IconsChanged { add { } remove { } }
        public event Action? ScanStateChanged { add { } remove { } }
    }
    private sealed class FixedMonitor(HardwareInfoSnapshot initial) : Nepply.Services.Telemetry.IHardwareMonitor
    {
        private HardwareInfoSnapshot _snapshot = initial;
        public event Action<HardwareInfoSnapshot>? HardwareUpdated;
        public void Publish(HardwareInfoSnapshot snapshot) { _snapshot = snapshot; HardwareUpdated?.Invoke(snapshot); }
        public void Configure(AppSettings settings, bool visible) { }
        public HardwareInfoSnapshot GetSnapshot() => _snapshot;
        public void Start() { }
        public void Dispose() { }
    }
    private sealed class Actions : IDesktopActions
    {
        public void Refresh() => throw new InvalidOperationException("Unexpected fixture action");
        public Task<UserMessage?> LaunchIconAsync(DesktopIcon icon) => throw new InvalidOperationException("Unexpected fixture action");
        public UserMessage? ShowShellContextMenu(DesktopIcon icon, IntPtr hwnd, Point? screenAnchor = null) => throw new InvalidOperationException("Unexpected fixture action");
    }
}
