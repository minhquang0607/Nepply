using System.IO;
using Nepply.Models;
using Nepply.Services;

internal static class ReleaseHardeningTests
{
    public static void Run(string output, Action<string,bool> check)
    {
        BrandMigrationTests.Run(output, check);
        SearchSessionTests.Run(check);
        PanelGeometryTests.Run(check);
        LayoutEditSessionTests.Run(check);
        WindowServicesTests.Run(check);
        SettingsEditSessionTests.Run(check);
        SettingsChangeSetTests.Run(check);
        DesktopCatalogStoreTests.Run(check);
        DesktopCollectionTests.Run(check);
        DesktopIncrementalTests.Run(check);
        DesktopScanOptionsTests.Run(check);
        IconGridGeometryTests.Run(check);
        AccentPaletteTests.Run(check);
        DesktopScanReplyTests.Run(check);
        SecurityBoundaryTests.Run(check);
        SecurityRegressionTests.Run(output,check);
        SamplingLifecycleTests.Run(check);
        PackageIdentityTests.Run(check);
        StartupCoordinatorTests.RunAsync(check).GetAwaiter().GetResult();
        foreach(var (key,label) in new[] {(32,"Space"),(37,"Left"),(38,"Up"),(39,"Right"),(40,"Down"),(36,"Home"),(35,"End"),(33,"PageUp"),(34,"PageDown"),(48,"0"),(90,"Z")})
            check("Hotkey formats virtual key "+key,Nepply.Helpers.HotkeyGesture.Format(7,key)=="Ctrl + Alt + Shift + "+label);
        check("Startup Run command matches exact quoted application",AutoStartService.MatchesRunCommand("\"C:\\Apps\\Nepply.exe\"",@"c:\Apps\Nepply.exe"));
        check("Startup Run command for another install is preserved",!AutoStartService.MatchesRunCommand("\"D:\\Other\\Nepply.exe\"",@"C:\Apps\Nepply.exe"));
        check("Startup Run command with arguments is preserved",!AutoStartService.MatchesRunCommand("\"C:\\Apps\\Nepply.exe\" --custom",@"C:\Apps\Nepply.exe"));
        check("Non-string Startup Run value is not owned",!AutoStartService.MatchesRunCommand(42,@"C:\Apps\Nepply.exe"));
        check("Unquoted Startup Run command is not claimed",!AutoStartService.MatchesRunCommand(@"C:\Apps\Nepply.exe",@"C:\Apps\Nepply.exe"));
        check("Startup shortcut requires exact application path",AutoStartService.MatchesCurrentApplication(@"C:\Apps\Nepply.exe","",@"c:\apps\Nepply.exe"));
        check("Startup shortcut with custom arguments is preserved",!AutoStartService.MatchesCurrentApplication(@"C:\Apps\Nepply.exe","--custom",@"C:\Apps\Nepply.exe"));
        check("Startup shortcut for another install is preserved",!AutoStartService.MatchesCurrentApplication(@"D:\Other\Nepply.exe","",@"C:\Apps\Nepply.exe"));
        check("Unresolved Startup shortcut is preserved",!AutoStartService.MatchesCurrentApplication("Nepply.exe","",@"C:\Apps\Nepply.exe"));
        SensorSelectionTests.Run(check);
        check("Application Control error uses numeric code",HelperFailure.IsPolicyBlocked(new System.ComponentModel.Win32Exception(4551,"localized message")));
        check("Access denied is not assumed to be Application Control",!HelperFailure.IsPolicyBlocked(new System.ComponentModel.Win32Exception(5)));
        check("Error message text alone cannot trigger policy classification",!HelperFailure.IsPolicyBlocked(new Exception("4551 Application Control")));
        SettingsSearchTests.Run(check);
        SensorDiagnosticReportTests.Run(check);
        AppLoggerTests.Run(check);
        var root = Path.Combine(output,"isolated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        DesktopReliabilityTests.Run(root,check);
        DesktopWatcherTests.Run(root,check);
        LayoutProfileTests.Run(check);
        TelemetryFreshnessTests.Run(check);
        SensorWireTests.Run(check);
        var debounce = new BoundedDebounce();
        check("Desktop burst starts with quiet interval",debounce.Signal(1000)==350);
        check("Continuous Desktop changes respect maximum delay",debounce.Signal(2850)==150 && debounce.Signal(3000)==0 && debounce.Signal(3100)==0);
        debounce.Reset();
        check("New Desktop burst receives fresh quiet interval",debounce.Signal(3200)==350);
        var path = Path.Combine(root,"settings.json");
        var store = new SettingsStore(path);
        var fresh = store.Load();
        check("Fresh install keeps Desktop visible before onboarding",!fresh.HasCompletedOnboarding && !fresh.HideDesktopIcons);
        File.WriteAllText(path,"{\"HideDesktopIcons\":true,\"EdgePosition\":1}");
        var legacy = store.Load();
        check("Existing settings migrate without repeating onboarding",legacy.HasCompletedOnboarding && legacy.HideDesktopIcons && legacy.EdgePosition == EdgePosition.Right);
        var retiredPath=Path.Combine(root,"retired-details.json");
        File.WriteAllText(retiredPath,"{\"ShowStorage\":true,\"ShowDetails\":true,\"WidgetOrder\":[\"details\",\"network\"]}");
        var retired=new SettingsStore(retiredPath).Load();
        check("Retired detail settings load without reviving removed widget",!retired.WidgetOrder.Contains("details"));
        bool failedSave = false;
        try { OnboardingService.Complete(fresh,EdgePosition.Top,true,_ => throw new IOException("Simulated failure")); }
        catch(IOException) { failedSave = true; }
        check("Onboarding failed save leaves live settings unchanged",failedSave && !fresh.HasCompletedOnboarding && !fresh.HideDesktopIcons && fresh.EdgePosition == EdgePosition.Left);
        OnboardingService.Complete(fresh,EdgePosition.Top,true,store.Save);
        var onboarded = store.Load();
        var resetDraft=onboarded.Clone(); resetDraft.ResetToDefaults();
        check("Reset preferences keeps completed onboarding",resetDraft.HasCompletedOnboarding && !resetDraft.HideDesktopIcons && resetDraft.EdgePosition==EdgePosition.Left);
        check("Onboarding persists explicit choice atomically",fresh.HasCompletedOnboarding && onboarded.HasCompletedOnboarding && onboarded.HideDesktopIcons && onboarded.EdgePosition == EdgePosition.Top);
        store.Save(new AppSettings { PanelWidth = 410 });
        store.Save(new AppSettings { PanelWidth = 450 });
        check("Settings atomic save keeps previous backup",File.Exists(path+".bak") && store.Load().PanelWidth == 450);
        File.WriteAllText(path,"{broken");
        check("Corrupt settings restores last good backup",store.Load().PanelWidth == 410 && store.RecoveryNotice?.Key == "RecoveryRestored");
        check("Corrupt original retained for diagnosis",Directory.GetFiles(root,"*.broken-*").Length == 1);
        store.Save(new AppSettings { PanelWidth = 420 });
        check("Recovered settings can save",store.Load().PanelWidth == 420);
        File.WriteAllText(path,"{\"SchemaVersion\":999,\"PanelWidth\":500}");
        var future = new SettingsStore(path); future.Load();
        check("Future settings expose a language-neutral recovery reason",future.RecoveryNotice?.Key == "RecoveryNewer");
        bool blocked=false;
        try { future.Save(new()); } catch(IOException) { blocked=true; }
        check("Future schema never overwritten",blocked && File.ReadAllText(path).Contains("999"));
        foreach (var schemaVersion in new long[] { 2, (long)int.MaxValue + 1 })
        {
            var futurePath=Path.Combine(root,"future-types-"+schemaVersion+".json");
            var futureJson="{\"SchemaVersion\":"+schemaVersion+",\"PanelWidth\":{\"changedType\":true}}";
            File.WriteAllText(futurePath,futureJson);
            var futureStore=new SettingsStore(futurePath); futureStore.Load();
            bool saveRejected=false;
            try { futureStore.Save(new()); } catch(IOException) { saveRejected=true; }
            check("Future incompatible fields remain untouched: "+schemaVersion,saveRejected && File.Exists(futurePath) && File.ReadAllText(futurePath)==futureJson && Directory.GetFiles(root,Path.GetFileName(futurePath)+".broken-*").Length==0);
        }
        var boundedPath=Path.Combine(root,"bounded-save.json");
        var boundedStore=new SettingsStore(boundedPath);
        boundedStore.Save(new AppSettings { PanelWidth=432 });
        string originalSettings=File.ReadAllText(boundedPath);
        bool hugeSaveRejected=false;
        try { boundedStore.Save(new AppSettings { IconOrder=[new string('x',SettingsStore.MaximumBytes)] }); }
        catch(InvalidDataException) { hugeSaveRejected=true; }
        check("Oversized save preserves readable last configuration",hugeSaveRejected && File.ReadAllText(boundedPath)==originalSettings && boundedStore.Load().PanelWidth==432 && Directory.GetFiles(root,"bounded-save.json.tmp-*").Length==0);
        var oversizedPath = Path.Combine(root,"oversized.json");
        File.WriteAllText(oversizedPath,new string(' ',2 * 1024 * 1024 + 1));
        var oversized = new SettingsStore(oversizedPath);
        oversized.Load();
        check("Oversized settings recover without crashing",oversized.RecoveryNotice?.Key == "RecoveryDamaged" && !File.Exists(oversizedPath) && Directory.GetFiles(root,"oversized.json.broken-*").Length == 1);
        check("Recovery defaults do not hide Desktop",!oversized.Load().HideDesktopIcons);
        var backupPath = Path.Combine(root,"future-backup.json");
        File.WriteAllText(backupPath + ".bak","{\"SchemaVersion\":999}");
        var backupStore = new SettingsStore(backupPath); backupStore.Load();
        check("Future backup exposes its own recovery reason",backupStore.RecoveryNotice?.Key == "RecoveryBackupNewer");
        bool backupBlocked = false;
        try { backupStore.Save(new()); } catch(IOException) { backupBlocked = true; }
        check("Future backup schema never overwritten",backupBlocked && !File.Exists(backupPath) && File.ReadAllText(backupPath + ".bak").Contains("999"));
        var fake = new FakeDesktop();
        var journal=Path.Combine(root,"desktop.lease");
        var lease = new DesktopVisibilityLease(fake,journal);
        check("Visible Desktop is journaled before hide",lease.Hide() && lease.OwnsChange && fake.Visible == false);
        var afterCrash = new DesktopVisibilityLease(fake,journal);
        check("New session restores previous hidden Desktop",afterCrash.Restore() && fake.Visible == true && !afterCrash.OwnsChange);
        fake.Visible=false;
        check("User-hidden Desktop is not claimed",!lease.Hide() && !lease.OwnsChange);
        check("User-hidden Desktop stays hidden",lease.Restore() && fake.Visible == false);
        fake.Visible=true; lease.Hide(); fake.Visible=null;
        check("Unavailable Explorer preserves recovery journal",!lease.Restore() && lease.OwnsChange);
        fake.Visible=false; fake.Fail=true;
        check("Failed restore preserves recovery journal",!lease.Restore() && lease.OwnsChange);
        fake.Fail=false; lease.Restore();
        using (var visibilityService = new DesktopIconService(lease))
        {
            fake.ThrowAfterHide = true;
            check("Hide exception is reported and retains recovery ownership",
                visibilityService.HideDesktopIcons() != null && visibilityService.AreDesktopIconsHidden && fake.Visible == false);
        }
        check("Dispose restores Desktop after hide changed state then threw",fake.Visible == true && !lease.OwnsChange);
        fake.ThrowAfterHide = false;
        using (var visibilityService = new DesktopIconService(lease))
        {
            fake.Visible = false;
            check("Already hidden Desktop succeeds without claiming ownership",
                visibilityService.HideDesktopIcons() == null && !visibilityService.AreDesktopIconsHidden);
            fake.Visible = null;
            check("Unavailable Explorer reports hide failure",visibilityService.HideDesktopIcons() != null && !lease.OwnsChange);
            fake.Visible = true;
            visibilityService.HideDesktopIcons();
            fake.Fail = true;
            check("Failed restoration reports error and preserves recovery intent",
                visibilityService.ShowDesktopIcons() != null && lease.OwnsChange);
            fake.Fail = false;
            check("Successful retry clears recovery intent",visibilityService.ShowDesktopIcons() == null && !lease.OwnsChange);
        }
    }
    private sealed class FakeDesktop : IDesktopVisibility
    {
        public bool? Visible=true;
        public bool Fail;
        public bool ThrowAfterHide;
        public bool? IsVisible()=>Visible;
        public bool SetVisible(bool visible)
        {
            if(Fail)return false;
            Visible=visible;
            if (!visible && ThrowAfterHide) throw new IOException("Simulated failure after Explorer changed visibility");
            return true;
        }
    }
}
