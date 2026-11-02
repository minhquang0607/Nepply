using System.IO;
using Nepply.Models;
using Nepply.Services;

internal static class ReleaseHardeningTests
{
    private static void SetupChoices(Action<string, bool> check)
    {
        string path = @"Software\Nepply-Tests\" + Guid.NewGuid().ToString("N");
        try
        {
            AppSettings Run(bool onboarded, string language, int autoStart, out int saves)
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(path))
                {
                    key.SetValue("Language", language);
                    key.SetValue("AutoStart", autoStart, Microsoft.Win32.RegistryValueKind.DWord);
                }
                var settings = new AppSettings { Language = "en", HasCompletedOnboarding = onboarded };
                int count = 0;
                InstallerChoices.Apply(settings, () => count++, path);
                saves = count;
                return settings;
            }
            var first = Run(false, "ja", 1, out int firstSaves);
            check("Setup language reaches the first run", first.Language == "ja" && firstSaves == 1);
            check("Setup startup choice reaches settings", first.AutoStart);
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(path)!)
                check("Setup choices are read once", key.GetValue("Language") == null && key.GetValue("AutoStart") == null);
            check("Reinstalling keeps the language chosen in Nepply", Run(true, "ja", 0, out _).Language == "en");
            check("Unknown Setup language is ignored", Run(false, "xx", 0, out int unknownSaves).Language == "en" && unknownSaves == 0);
            check("Setup without the startup task leaves startup off", !Run(false, "de", 0, out _).AutoStart);
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(path))
                key.SetValue("AutoStart", 0, Microsoft.Win32.RegistryValueKind.DWord);
            var kept = new AppSettings { AutoStart = true };
            InstallerChoices.Apply(kept, () => { }, path);
            check("Unticking startup in Setup overrules settings kept from an earlier install", !kept.AutoStart);
            var untouched = new AppSettings { Language = "en" };
            InstallerChoices.Apply(untouched, () => throw new InvalidOperationException(), path + "-missing");
            check("A run without Setup choices changes nothing", untouched.Language == "en");
        }
        finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Nepply-Tests", false); }
    }

    public static void Run(string output, Action<string, bool> check)
    {
        BrandMigrationTests.Run(output, check);
        SearchSessionTests.Run(check);
        PanelGeometryTests.Run(check);
        LayoutEditSessionTests.Run(check);
        WindowServicesTests.Run(check);
        SettingsEditSessionTests.Run(check);
        SettingsChangeSetTests.Run(check);
        ShortcutBindingTests.Run(check);
        DesktopCatalogStoreTests.Run(check);
        DesktopCollectionTests.Run(check);
        DesktopIncrementalTests.Run(check);
        DesktopScanOptionsTests.Run(check);
        IconGridGeometryTests.Run(check);
        AccentPaletteTests.Run(check);
        DesktopScanReplyTests.Run(check);
        SecurityBoundaryTests.Run(check);
        BackgroundUpdateCheckTests.Run(check);
        SecurityRegressionTests.Run(output, check);
        SamplingLifecycleTests.Run(check);
        PackageIdentityTests.Run(check);
        StartupCoordinatorTests.RunAsync(check).GetAwaiter().GetResult();
        foreach (var (key, label) in new[] { (32, "Space"), (37, "Left"), (38, "Up"), (39, "Right"), (40, "Down"), (36, "Home"), (35, "End"), (33, "PageUp"), (34, "PageDown"), (48, "0"), (90, "Z") })
            check("Hotkey formats virtual key " + key, Nepply.Helpers.HotkeyGesture.Format(7, key) == "Ctrl + Alt + Shift + " + label);
        check("Startup Run command matches exact quoted application", AutoStartService.MatchesRunCommand("\"C:\\Apps\\Nepply.exe\"", @"c:\Apps\Nepply.exe"));
        check("Startup Run command for another install is preserved", !AutoStartService.MatchesRunCommand("\"D:\\Other\\Nepply.exe\"", @"C:\Apps\Nepply.exe"));
        check("Startup Run command with arguments is preserved", !AutoStartService.MatchesRunCommand("\"C:\\Apps\\Nepply.exe\" --custom", @"C:\Apps\Nepply.exe"));
        check("Non-string Startup Run value is not owned", !AutoStartService.MatchesRunCommand(42, @"C:\Apps\Nepply.exe"));
        check("Unquoted Startup Run command is not claimed", !AutoStartService.MatchesRunCommand(@"C:\Apps\Nepply.exe", @"C:\Apps\Nepply.exe"));
        Func<string, bool> missing = _ => false, present = _ => true, fixedDrive = _ => true, offlineDrive = _ => false;
        check("Dead Startup Run entry of moved copy can be retired", AutoStartService.IsDeadRunCommand("\"D:\\Old\\Nepply.exe\"", "Nepply.exe", missing, fixedDrive));
        check("Dead legacy Startup Run entry can be retired", AutoStartService.IsDeadRunCommand("\"D:\\Old\\HoverBox.exe\"", "HoverBox.exe", missing, fixedDrive));
        check("Live Startup Run entry of another copy is preserved", !AutoStartService.IsDeadRunCommand("\"D:\\Other\\Nepply.exe\"", "Nepply.exe", present, fixedDrive));
        check("Startup Run entry on offline or removable drive is preserved", !AutoStartService.IsDeadRunCommand("\"E:\\Usb\\Nepply.exe\"", "Nepply.exe", missing, offlineDrive));
        check("Startup Run entry on network share is preserved", !AutoStartService.IsDeadRunCommand("\"\\\\server\\apps\\Nepply.exe\"", "Nepply.exe", missing, fixedDrive));
        check("Missing Startup Run entry with arguments is preserved", !AutoStartService.IsDeadRunCommand("\"D:\\Old\\Nepply.exe\" --custom", "Nepply.exe", missing, fixedDrive));
        check("Missing unquoted Startup Run entry is preserved", !AutoStartService.IsDeadRunCommand(@"D:\Old\Nepply.exe", "Nepply.exe", missing, fixedDrive));
        check("Missing Startup Run entry for another program is preserved", !AutoStartService.IsDeadRunCommand("\"D:\\Old\\Other.exe\"", "Nepply.exe", missing, fixedDrive));
        check("Missing relative Startup Run entry is preserved", !AutoStartService.IsDeadRunCommand("\"Old\\Nepply.exe\"", "Nepply.exe", missing, fixedDrive));
        check("Non-string Startup Run value is never dead", !AutoStartService.IsDeadRunCommand(42, "Nepply.exe", missing, fixedDrive));
        string vanished = Path.Combine(Path.GetTempPath(), "nepply-moved-" + Guid.NewGuid().ToString("N"), "Nepply.exe");
        check("Default dead-entry probe detects missing copy on fixed drive",
            Path.GetPathRoot(vanished) is not { } tempRoot || new DriveInfo(tempRoot).DriveType != DriveType.Fixed || AutoStartService.IsDeadRunCommand("\"" + vanished + "\"", "Nepply.exe"));
        check("Default dead-entry probe keeps this live executable", !AutoStartService.IsDeadRunCommand("\"" + Environment.ProcessPath + "\"", Path.GetFileName(Environment.ProcessPath!)));
        check("Startup shortcut requires exact application path", AutoStartService.MatchesCurrentApplication(@"C:\Apps\Nepply.exe", "", @"c:\apps\Nepply.exe"));
        check("Startup shortcut with custom arguments is preserved", !AutoStartService.MatchesCurrentApplication(@"C:\Apps\Nepply.exe", "--custom", @"C:\Apps\Nepply.exe"));
        check("Startup shortcut for another install is preserved", !AutoStartService.MatchesCurrentApplication(@"D:\Other\Nepply.exe", "", @"C:\Apps\Nepply.exe"));
        check("Unresolved Startup shortcut is preserved", !AutoStartService.MatchesCurrentApplication("Nepply.exe", "", @"C:\Apps\Nepply.exe"));
        SensorSelectionTests.Run(check);
        check("Application Control error uses numeric code", HelperFailure.IsPolicyBlocked(new System.ComponentModel.Win32Exception(4551, "localized message")));
        check("Access denied is not assumed to be Application Control", !HelperFailure.IsPolicyBlocked(new System.ComponentModel.Win32Exception(5)));
        check("Error message text alone cannot trigger policy classification", !HelperFailure.IsPolicyBlocked(new Exception("4551 Application Control")));
        SettingsSearchTests.Run(check);
        SensorDiagnosticReportTests.Run(check);
        AppLoggerTests.Run(check);
        var root = Path.Combine(output, "isolated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        DesktopReliabilityTests.Run(root, check);
        DesktopWatcherTests.Run(root, check);
        LayoutProfileTests.Run(check);
        TelemetryFreshnessTests.Run(check);
        SensorWireTests.Run(check);
        var debounce = new BoundedDebounce();
        check("Desktop burst starts with quiet interval", debounce.Signal(1000) == 350);
        check("Continuous Desktop changes respect maximum delay", debounce.Signal(2850) == 150 && debounce.Signal(3000) == 0 && debounce.Signal(3100) == 0);
        debounce.Reset();
        check("New Desktop burst receives fresh quiet interval", debounce.Signal(3200) == 350);
        var path = Path.Combine(root, "settings.json");
        var store = new SettingsStore(path);
        var fresh = store.Load();
        check("Fresh install keeps Desktop visible before onboarding", !fresh.HasCompletedOnboarding && !fresh.HideDesktopIcons);
        check("Fresh install starts in the Windows display language", fresh.Language == LocalizationService.SystemDefault);
        foreach (var (culture, expected) in new[] {("vi-VN","vi"),("en-US","en"),("en-GB","en"),("ja-JP","ja"),("de-AT","de"),("zh-CN","zh"),("zh-SG","zh"),("ru-RU","ru"),("fr-CA","fr"),("th-TH","th"),
            ("zh-TW","en"),("zh-HK","en"),("zh-Hant","en"),("ko-KR","en"),("pt-BR","en")})
            check($"Windows language {culture} opens Nepply in {expected}", LocalizationService.DefaultFor(new System.Globalization.CultureInfo(culture)) == expected);
        check("Invariant Windows language falls back to English", LocalizationService.DefaultFor(System.Globalization.CultureInfo.InvariantCulture) == "en");
        var languagePath = Path.Combine(root, "language.json");
        File.WriteAllText(languagePath, "{\"HasCompletedOnboarding\":true,\"PanelWidth\":400}");
        check("Settings saved before the Windows default keep Vietnamese", new SettingsStore(languagePath).Load().Language == "vi");
        File.WriteAllText(languagePath, "{\"HasCompletedOnboarding\":true,\"Language\":\"ja\"}");
        check("Saved language choice wins over the Windows default", new SettingsStore(languagePath).Load().Language == "ja");
        var resetLanguage = new AppSettings { Language = "fr", PanelWidth = 500 }; resetLanguage.ResetToDefaults();
        check("Reset preferences keeps the chosen language", resetLanguage.Language == "fr" && resetLanguage.PanelWidth == new AppSettings().PanelWidth);
        var welcomeChoice = new AppSettings();
        OnboardingService.Complete(welcomeChoice, EdgePosition.Left, false, false, _ => { }, "de");
        check("Welcome can turn the daily update check off", !welcomeChoice.AutoCheckUpdates);
        SetupChoices(check);
        check("Windows startup off state is recognised", AutoStartService.IsStartupStateOff(new byte[] { 3, 0, 0, 0 }));
        check("Windows startup on state is recognised", !AutoStartService.IsStartupStateOff(new byte[] { 2, 0, 0, 0 }));
        check("Missing Windows startup state is not off", !AutoStartService.IsStartupStateOff(null) && !AutoStartService.IsStartupStateOff(Array.Empty<byte>()));
        bool shown = true; var commands = new List<int>();
        var iconWindow = new WindowDesktopVisibility(() => (IntPtr)7, _ => shown, (_, command) => { commands.Add(command); shown = command != 0; return true; });
        check("The Store edition hides the icon window itself", iconWindow.SetVisible(false) && !shown && commands.SequenceEqual([0]));
        check("The Store edition shows icons without taking focus", iconWindow.SetVisible(true) && shown && commands.SequenceEqual([0, 8]));
        var noDesktop = new WindowDesktopVisibility(() => IntPtr.Zero, _ => true, (_, _) => throw new InvalidOperationException());
        check("A missing Desktop is reported, never touched", noDesktop.IsVisible() == null && !noDesktop.SetVisible(false));
        var stubborn = new WindowDesktopVisibility(() => (IntPtr)7, _ => true, (_, _) => true);
        check("A hide that did not happen is reported as failed", !stubborn.SetVisible(false));
        string approvedPath = @"Software\Nepply-Tests\" + Guid.NewGuid().ToString("N");
        try
        {
            using (var approved = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(approvedPath))
            {
                approved.SetValue("Nepply", new byte[] { 3, 0, 0, 0 });
                approved.SetValue("Other", new byte[] { 3, 0, 0, 0 });
            }
            AutoStartService.ForgetOrphanedState(approvedPath);
            using (var approved = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(approvedPath)!)
                check("A startup switch left by a removed entry is forgotten, others stay", approved.GetValue("Nepply") == null && approved.GetValue("Other") != null);
            AutoStartService.ForgetOrphanedState(approvedPath + "-missing");
            check("Forgetting a startup switch tolerates a missing key", true);
        }
        finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Nepply-Tests", false); }
        check("Onboarding saves the language picked on the welcome screen", welcomeChoice.Language == "de" && welcomeChoice.HasCompletedOnboarding);
        File.WriteAllText(path, "{\"HideDesktopIcons\":true,\"EdgePosition\":1}");
        var legacy = store.Load();
        check("Existing settings migrate without repeating onboarding", legacy.HasCompletedOnboarding && legacy.HideDesktopIcons && legacy.EdgePosition == EdgePosition.Right);
        var retiredPath = Path.Combine(root, "retired-details.json");
        File.WriteAllText(retiredPath, "{\"ShowStorage\":true,\"ShowDetails\":true,\"WidgetOrder\":[\"details\",\"network\"]}");
        var retired = new SettingsStore(retiredPath).Load();
        check("Retired detail settings load without reviving removed widget", !retired.WidgetOrder.Contains("details"));
        bool failedSave = false;
        try { OnboardingService.Complete(fresh, EdgePosition.Top, true, true, _ => throw new IOException("Simulated failure")); }
        catch (IOException) { failedSave = true; }
        check("Onboarding failed save leaves live settings unchanged", failedSave && !fresh.HasCompletedOnboarding && !fresh.HideDesktopIcons && fresh.EdgePosition == EdgePosition.Left);
        OnboardingService.Complete(fresh, EdgePosition.Top, true, true, store.Save);
        var onboarded = store.Load();
        var resetDraft = onboarded.Clone(); resetDraft.ResetToDefaults();
        check("Reset preferences keeps completed onboarding", resetDraft.HasCompletedOnboarding && !resetDraft.HideDesktopIcons && resetDraft.EdgePosition == EdgePosition.Left);
        check("Onboarding persists explicit choice atomically", fresh.HasCompletedOnboarding && onboarded.HasCompletedOnboarding && onboarded.HideDesktopIcons && onboarded.EdgePosition == EdgePosition.Top);
        store.Save(new AppSettings { PanelWidth = 410 });
        store.Save(new AppSettings { PanelWidth = 450 });
        check("Settings atomic save keeps previous backup", File.Exists(path + ".bak") && store.Load().PanelWidth == 450);
        File.WriteAllText(path, "{broken");
        check("Corrupt settings restores last good backup", store.Load().PanelWidth == 410 && store.RecoveryNotice?.Key == "RecoveryRestored");
        check("Corrupt original retained for diagnosis", Directory.GetFiles(root, "*.broken-*").Length == 1);
        store.Save(new AppSettings { PanelWidth = 420 });
        check("Recovered settings can save", store.Load().PanelWidth == 420);
        File.WriteAllText(path, "{\"SchemaVersion\":999,\"PanelWidth\":500}");
        var future = new SettingsStore(path); future.Load();
        check("Future settings expose a language-neutral recovery reason", future.RecoveryNotice?.Key == "RecoveryNewer");
        bool blocked = false;
        try { future.Save(new()); } catch (IOException) { blocked = true; }
        check("Future schema never overwritten", blocked && File.ReadAllText(path).Contains("999"));
        foreach (var schemaVersion in new long[] { 2, (long)int.MaxValue + 1 })
        {
            var futurePath = Path.Combine(root, "future-types-" + schemaVersion + ".json");
            var futureJson = "{\"SchemaVersion\":" + schemaVersion + ",\"PanelWidth\":{\"changedType\":true}}";
            File.WriteAllText(futurePath, futureJson);
            var futureStore = new SettingsStore(futurePath); futureStore.Load();
            bool saveRejected = false;
            try { futureStore.Save(new()); } catch (IOException) { saveRejected = true; }
            check("Future incompatible fields remain untouched: " + schemaVersion, saveRejected && File.Exists(futurePath) && File.ReadAllText(futurePath) == futureJson && Directory.GetFiles(root, Path.GetFileName(futurePath) + ".broken-*").Length == 0);
        }
        var boundedPath = Path.Combine(root, "bounded-save.json");
        var boundedStore = new SettingsStore(boundedPath);
        boundedStore.Save(new AppSettings { PanelWidth = 432 });
        string originalSettings = File.ReadAllText(boundedPath);
        bool hugeSaveRejected = false;
        try { boundedStore.Save(new AppSettings { IconOrder = [new string('x', SettingsStore.MaximumBytes)] }); }
        catch (InvalidDataException) { hugeSaveRejected = true; }
        check("Oversized save preserves readable last configuration", hugeSaveRejected && File.ReadAllText(boundedPath) == originalSettings && boundedStore.Load().PanelWidth == 432 && Directory.GetFiles(root, "bounded-save.json.tmp-*").Length == 0);
        var oversizedPath = Path.Combine(root, "oversized.json");
        // Valid JSON, so only the size limit can reject it.
        File.WriteAllText(oversizedPath, "{\"PanelWidth\":432,\"IconOrder\":[\"" + new string('x', SettingsStore.MaximumBytes) + "\"]}");
        var oversized = new SettingsStore(oversizedPath);
        oversized.Load();
        check("Oversized settings recover without crashing", oversized.RecoveryNotice?.Key == "RecoveryDamaged" && !File.Exists(oversizedPath) && Directory.GetFiles(root, "oversized.json.broken-*").Length == 1);
        check("Recovery defaults do not hide Desktop", !oversized.Load().HideDesktopIcons);
        var backupPath = Path.Combine(root, "future-backup.json");
        File.WriteAllText(backupPath + ".bak", "{\"SchemaVersion\":999}");
        var backupStore = new SettingsStore(backupPath); backupStore.Load();
        check("Future backup exposes its own recovery reason", backupStore.RecoveryNotice?.Key == "RecoveryBackupNewer");
        bool backupBlocked = false;
        try { backupStore.Save(new()); } catch (IOException) { backupBlocked = true; }
        check("Future backup schema never overwritten", backupBlocked && !File.Exists(backupPath) && File.ReadAllText(backupPath + ".bak").Contains("999"));
        var fake = new FakeDesktop();
        var journal = Path.Combine(root, "desktop.lease");
        var lease = new DesktopVisibilityLease(fake, journal);
        check("Visible Desktop is journaled before hide", lease.Hide() && lease.OwnsChange && fake.Visible == false);
        var afterCrash = new DesktopVisibilityLease(fake, journal);
        check("New session restores previous hidden Desktop", afterCrash.Restore() && fake.Visible == true && !afterCrash.OwnsChange);
        fake.Visible = false;
        check("User-hidden Desktop is not claimed", !lease.Hide() && !lease.OwnsChange);
        check("User-hidden Desktop stays hidden", lease.Restore() && fake.Visible == false);
        fake.Visible = true; lease.Hide(); fake.Visible = null;
        check("Unavailable Explorer preserves recovery journal", !lease.Restore() && lease.OwnsChange);
        fake.Visible = false; fake.Fail = true;
        check("Failed restore preserves recovery journal", !lease.Restore() && lease.OwnsChange);
        fake.Fail = false; lease.Restore();
        using (var visibilityService = new DesktopIconService(lease))
        {
            fake.ThrowAfterHide = true;
            check("Hide exception is reported and retains recovery ownership",
                visibilityService.HideDesktopIcons() != null && visibilityService.AreDesktopIconsHidden && fake.Visible == false);
        }
        check("Dispose restores Desktop after hide changed state then threw", fake.Visible == true && !lease.OwnsChange);
        fake.ThrowAfterHide = false;
        using (var visibilityService = new DesktopIconService(lease))
        {
            fake.Visible = false;
            check("Already hidden Desktop succeeds without claiming ownership",
                visibilityService.HideDesktopIcons() == null && !visibilityService.AreDesktopIconsHidden);
            fake.Visible = null;
            check("Unavailable Explorer reports hide failure", visibilityService.HideDesktopIcons() != null && !lease.OwnsChange);
            fake.Visible = true;
            visibilityService.HideDesktopIcons();
            fake.Fail = true;
            check("Failed restoration reports error and preserves recovery intent",
                visibilityService.ShowDesktopIcons() != null && lease.OwnsChange);
            fake.Fail = false;
            check("Successful retry clears recovery intent", visibilityService.ShowDesktopIcons() == null && !lease.OwnsChange);
        }
    }
    private sealed class FakeDesktop : IDesktopVisibility
    {
        public bool? Visible = true;
        public bool Fail;
        public bool ThrowAfterHide;
        public bool? IsVisible() => Visible;
        public bool SetVisible(bool visible)
        {
            if (Fail) return false;
            Visible = visible;
            if (!visible && ThrowAfterHide) throw new IOException("Simulated failure after Explorer changed visibility");
            return true;
        }
    }
}
