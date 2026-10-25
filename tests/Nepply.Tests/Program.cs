using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nepply;
using Nepply.Models;
using Nepply.Services;
using Nepply.ViewModels;
using Nepply.Views;
using Nepply.Modules.Search;
using Nepply.Modules.Desktop;

internal static class Program
{
    private static readonly List<string> Results = [];
    private static int _failures;
    private static int _passed;
    private static bool _streamResults;
    private static void Check(string name, bool valid)
    {
        Results.Add($"{(valid ? "PASS" : "FAIL")} {name}");
        if (_streamResults) Console.WriteLine(Results[^1]);
        if (!valid) _failures++;
        else _passed++;
    }
    [STAThread]
    public static int Main(string[] args)
    {
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/qa");
        Directory.CreateDirectory(output);
        if (args.Contains("--update-ui-only"))
        {
            var updateApp = new App(testHost:true); updateApp.InitializeComponent();
            foreach (var theme in new[] { ThemeOption.Dark, ThemeOption.Light })
            {
                ThemeService.Apply(new AppSettings { Theme = theme });
                string themeOutput = Path.Combine(output, theme.ToString()); Directory.CreateDirectory(themeOutput);
                UpdateUiTests.Run(themeOutput, Check, Pump);
            }
            Results.Add($"RESULT: {_passed} passed; {_failures} failed. Scope: updater UI fixtures, dark/light themes; no real updates.");
            File.WriteAllLines(Path.Combine(output,"verification.txt"), Results);
            foreach (var result in Results) Console.WriteLine(result);
            return _failures == 0 ? 0 : 1;
        }
        if (args.Contains("--update-signature"))
        {
            int index = Array.IndexOf(args, "--update-signature");
            UpdateSignatureTests.Run(output, args[index + 1], args[index + 2], args[index + 3], args[index + 4], Check);
            Results.Add($"RESULT: {_passed} passed; {_failures} failed. Scope: Authenticode verifier only, not Nepply release signing.");
            File.WriteAllLines(Path.Combine(output,"verification.txt"), Results);
            foreach (var result in Results) Console.WriteLine(result);
            return _failures == 0 ? 0 : 1;
        }
        AppSettings.UseTestSettingsStore(Path.Combine(output,"test-settings.json"));
        if(args.Contains("--packaged-production-guardian"))return PackagedGuardianTests.Run(output,args.Contains("--wait-removal"));
        if(args.Contains("--verify-guardian-removal"))return PackagedGuardianTests.VerifyAfterRemoval(output);
        if(args.Contains("--production-guardian-crash-owner"))
            return ProductionGuardianCrashTests.Owner(output,Path.GetFullPath(args[^1]));
        if(args.Contains("--production-guardian-crash"))
            return ProductionGuardianCrashTests.Run(output,Path.GetFullPath(args[^1]));
        if(args.Contains("--production-guardian-runtime"))
            return ProductionGuardianRuntimeTests.Run(output,Path.GetFullPath(args[^1]));
        if(args.Contains("--recovery-state-only"))
        {
            DesktopRecoverySessionTests.Run(Check);
            GuardedDesktopVisibilityTests.Run(output,Check);
            DesktopGuardianDeploymentTests.Run(output,Check);
            DesktopRecoveryJournalTests.Run(output,Check);
            Results.Add($"RESULT: {_passed} passed; {_failures} failed. Recovery state fixtures only; no native Desktop, processes or named pipes.");
            File.WriteAllLines(Path.Combine(output,"verification.txt"),Results);
            foreach(var result in Results) Console.WriteLine(result);
            return _failures==0 ? 0 : 1;
        }
        if(args.Contains("--guardian-connection"))
        {
            DesktopGuardianConnectionTests.Run(Path.GetFullPath(args[^1]),Check).GetAwaiter().GetResult();
            Results.Add($"RESULT: {_passed} passed; {_failures} failed. Guardian transport only; never arms Desktop.");
            File.WriteAllLines(Path.Combine(output,"verification.txt"),Results);
            foreach(var result in Results) Console.WriteLine(result);
            return _failures==0 ? 0 : 1;
        }
        if(args.Contains("--guardian-external-owner")) return DesktopGuardianExperiment.ExternalOwner(output);
        if(args.Contains("--guardian-external-worker")) return DesktopGuardianExperiment.ExternalGuardian(output,int.Parse(args[^2]),long.Parse(args[^1]));
        if(args.Contains("--guardian-package-owner")) return DesktopGuardianExperiment.PackagedOwner(output);
        if(args.Contains("--guardian-file-worker")) return DesktopGuardianExperiment.FileGuardian(output,int.Parse(args[^2]),long.Parse(args[^1]));
        if(args.Contains("--guardian-owner")) return DesktopGuardianExperiment.Owner(output);
        if(args.Contains("--guardian-worker")) return DesktopGuardianExperiment.Guardian(output,int.Parse(args[^2]),long.Parse(args[^1]));
        if(args.Contains("--guardian-experiment")) return DesktopGuardianExperiment.Run(output);
        if(args.Contains("--packaged-startup-settings")) return PackagedStartupSettingsTests.Run(output);
        GpuIdentityTests.Run(Check);
        if (args.Contains("--gpu-identity"))
        {
            var nativeAdapters=Nepply.Services.Telemetry.GpuAdapterCatalog.Read();
            Check("Native DXCore graphics inventory available on QA machine",nativeAdapters.Count>0);
            foreach(var adapter in nativeAdapters) Results.Add("ADAPTER: "+adapter.Name+" | "+adapter.Kind);
            Results.Add($"RESULT: {_passed} passed; {_failures} failed. Scope: OS GPU identity only; no sensor engine or driver loading.");
            File.WriteAllLines(Path.Combine(output,"verification.txt"),Results);
            foreach(var result in Results) Console.WriteLine(result);
            return _failures==0 ? 0 : 1;
        }
        if(args.Contains("--desktop-discovery"))
        {
            int desktopExeIndex=Array.IndexOf(args,"--desktop-exe");
            return DesktopDiscoveryTests.Run(output,desktopExeIndex<0 ? null : Path.GetFullPath(args[desktopExeIndex+1]));
        }
        if(args.Contains("--interactive-ui")) return InteractiveUi.Run(output);
        if(args.Contains("--shell-notifications")) return DesktopShellNotificationTests.Run(output);
        if(args.Contains("--desktop-burst")) return DesktopBurstTests.Run(output);
        if(args.Contains("--desktop-pipeline"))
        {
            int executableIndex=Array.IndexOf(args,"--desktop-exe");
            if(executableIndex<0 || executableIndex+1>=args.Length) throw new ArgumentException("Desktop pipeline requires --desktop-exe.");
            return DesktopPipelineTests.Run(output,Path.GetFullPath(args[executableIndex+1]),args.Contains("--explorer-recovery"));
        }
        if(args.Contains("--native-icon-grid")) return NativeIconGridTests.Run(output,args.Contains("--mixed"));
        if(args.Contains("--ui-lifetime")) return UiLifetimeTests.Run(output);
        if(args.Contains("--ui-soak"))
        {
            int durationIndex=Array.IndexOf(args,"--minutes");
            if(durationIndex<0 || durationIndex+1>=args.Length || !int.TryParse(args[durationIndex+1],out int minutes))
                throw new ArgumentException("UI soak requires --minutes (1–480).");
            int profileIndex=Array.IndexOf(args,"--profile");
            if(profileIndex>=0 && profileIndex+1>=args.Length) throw new ArgumentException("Missing soak profile.");
            return UiSoakTests.Run(output,minutes,profileIndex<0 ? "normal" : args[profileIndex+1]);
        }
        if(args.Contains("--interactive-island")) return InteractiveUi.Run(output,island:true);
        if(args.Contains("--tray-ui")) return TrayMenuTests.Run(output);
        if(args.Contains("--desktop-visibility-runtime")) return DesktopVisibilityRuntimeTests.Run(output);
        if(args.Contains("--desktop-crash-child")) return DesktopCrashRuntimeTests.Child(output);
        if(args.Contains("--desktop-crash-runtime")) return DesktopCrashRuntimeTests.Run(output);
        if(args.Contains("--tray-registration")) return TrayRegistrationTests.Run(output,args.Contains("--explorer-recovery"));
        if(args.Contains("--dpi-layout")) return DpiLayoutTests.Run(output);
        if(args.Contains("--crash-lease-child")) return CrashRecoveryTests.Child(output);
        if(args.Contains("--crash-recovery")) return CrashRecoveryTests.Run(output);
        if(args.Contains("--telemetry-soak"))
        {
            int index=Array.IndexOf(args,"--sensor-exe");
            if(index<0 || index+1>=args.Length) throw new ArgumentException("Telemetry soak requires --sensor-exe.");
            return TelemetrySoak.Run(output,Path.GetFullPath(args[index+1]));
        }
        if(args.Contains("--appearance-only"))
        {
            _streamResults = true;
            var appearanceApp = new App(testHost: true); appearanceApp.InitializeComponent();
            VerifyPumpDeadline();
            ButtonAppearanceTests.Run(Check,Pump);
            InputAppearanceTests.Run(Check,Pump,output,Render);
            StateAppearanceTests.Run(Check,Pump,output,Render);
            MaterialAppearanceTests.Run(Check,Pump,output,Render);
            MonitorMaterialTests.Run(Check,Pump,output,Render);
            NeonInteractionTests.Run(Check,Pump,output,Render);
            LocalizationTests.Run(Check,Pump,output,Render);
            MotionPolicyTests.Run(Check,Pump);
            Results.Add($"RESULT: {_passed} passed; {_failures} failed. Scope: appearance fixture only; no native collectors or Desktop scan.");
            File.WriteAllLines(Path.Combine(output,"verification.txt"),Results);
            foreach(var result in Results) Console.WriteLine(result);
            return _failures==0 ? 0 : 1;
        }
        if(args.Contains("--preview-only"))
        {
            var previewApp=new App(testHost:true);previewApp.InitializeComponent();
            SettingsPreviewTests.Run(Check,Pump);
            MonitorSubscriptionTests.Run(Check,Pump);
            Results.Add($"RESULT: {_passed} passed; {_failures} failed. Scope: settings preview fixtures only.");
            File.WriteAllLines(Path.Combine(output,"verification.txt"),Results);
            foreach(var result in Results) Console.WriteLine(result);
            return _failures==0 ? 0 : 1;
        }
        UpdateTests.Run(Check);
        SignerRuleTests.Run(Check);
        LocalizationKeyTests.Run(Check);
        PointerActivationTests.Run(Check);
        DesktopRecoverySessionTests.Run(Check);
        GuardedDesktopVisibilityTests.Run(output,Check);
        DesktopGuardianDeploymentTests.Run(output,Check);
        DesktopRecoveryJournalTests.Run(output,Check);
        ReleaseHardeningTests.Run(output,Check);
        TelemetrySoak.VerifyAcceptance(Check);
        if(args.Contains("--core-only"))
        {
            Results.Add($"RESULT: {_passed} passed; {_failures} failed. Scope: core only; no UI rendering, native collector or installer validation.");
            File.WriteAllLines(Path.Combine(output,"verification.txt"),Results);
            foreach(var result in Results) Console.WriteLine(result);
            return _failures == 0 ? 0 : 1;
        }
        if (args.Contains("--video")) { VideoFrames.Capture(args[Array.IndexOf(args,"--video")+1],output); return 0; }
        Check("Rate uses actual elapsed time", SystemMetrics.CalculateRate(4096, 0, 2) == 2048);
        Check("Reset counter invalidates rate", SystemMetrics.CalculateRate(100, 200, 1) == null);
        Check("Resume gap invalidates rate", SystemMetrics.CalculateRate(4096, 0, 90) == null);
        Check("Zero interval invalidates rate", SystemMetrics.CalculateRate(4096, 0, 0) == null);
        Check("NaN interval invalidates rate", SystemMetrics.CalculateRate(4096, 0, double.NaN) == null);
        Check("Missing sensor remains missing", MetricValue.Valid(null, 0, 100) == null);
        Check("NaN sensor rejected", MetricValue.Valid(float.NaN, 0, 100) == null);
        Check("Infinity sensor rejected", MetricValue.Valid(float.PositiveInfinity, 0, 100) == null);
        Check("Zero RPM valid", MetricValue.Valid(0, 0, 30000) == 0);
        Check("Invalid temperature rejected", MetricValue.Valid(0, 1, 130) == null);
        Check("Missing network not formatted as zero", HardwareMonitorViewModel.FormatSpeed(null) == "—");
        Check("Speed units binary", HardwareMonitorViewModel.FormatSpeed(1048576).Contains("MiB/s"));
        var settings = new AppSettings { PanelWidth = -5, Theme = (ThemeOption)99, PanelOpacity = double.NaN, MonitorUpdateIntervalMs = 0 };
        settings.Validate();
        Check("Settings bounds and invalid enums", settings.PanelWidth == 320 && settings.Theme == ThemeOption.Dark && settings.PanelOpacity == 0.98 && settings.MonitorUpdateIntervalMs == 1000);
        var shiftOnly = new AppSettings { HotkeyModifiers = 4, HotkeyKey = 32 }; shiftOnly.Validate();
        var altShift = new AppSettings { HotkeyModifiers = 5, HotkeyKey = 32 }; altShift.Validate();
        Check("Saved hotkey without Ctrl or Alt falls back to Ctrl", shiftOnly.HotkeyModifiers == 2 && altShift.HotkeyModifiers == 5);
        Check("Every allowed hotkey modifier set includes Ctrl or Alt", new[] { 1, 2, 3, 5, 6, 7 }.All(value =>
            { var s = new AppSettings { HotkeyModifiers = value }; s.Validate(); return s.HotkeyModifiers == value; }));
        var oversized = new AppSettings { GpuIdentifier = new string('g', 2049), IntegratedGpuIdentifier = new string('i', 4096),
            MonitorDevice = new string('m', 5000), NetworkInterfaceId = new string('n', 3000) };
        oversized.Validate();
        var limitGpu = new string('g', AppSettings.MaximumIdentifierLength);
        var atLimit = new AppSettings { GpuIdentifier = limitGpu, NetworkInterfaceId = null! }; atLimit.Validate();
        Check("Oversized device identifiers fall back to automatic selection", oversized.GpuIdentifier == "" && oversized.IntegratedGpuIdentifier == ""
            && oversized.MonitorDevice == "" && oversized.NetworkInterfaceId == "" && atLimit.GpuIdentifier == limitGpu && atLimit.NetworkInterfaceId == "");
        var copy = settings.Clone(); copy.PanelWidth = 500;
        Check("Settings draft isolated", settings.PanelWidth == 320);
        var legacy = new AppSettings { PanelHeight = 520, GridCellWidth = double.NaN, GridCellHeight = double.PositiveInfinity }; legacy.Validate();
        Check("Legacy layout migrates once", legacy.PanelHeight == 320 && legacy.LayoutVersion == 1);
        Check("Invalid grid values normalized", legacy.GridCellWidth == 88 && legacy.GridCellHeight == 90);
        legacy.PanelHeight = 520; legacy.Validate();
        Check("Custom height survives subsequent validation", legacy.PanelHeight == 520);
        Check("Disk percentage derived", new DiskDriveInfo("C:", "System", 50, 200).UsagePercent == 25);
        var screen = new Nepply.Helpers.DisplayScreen("test", -1920, 0, 0, 1080, 1, false);
        Check("Edge rectangle excludes adjacent display", screen.Contains(-1, 200) && !screen.Contains(0, 200) && !screen.Contains(-1921, 200));
        Check("Snapshot collection immutable", typeof(HardwareInfoSnapshot).GetProperty("Drives")!.PropertyType == typeof(ImmutableArray<DiskDriveInfo>));

        var memory = SystemMetrics.ReadMemory();
        Check("Windows RAM coherent", memory.Total > 0 && memory.Used >= 0 && memory.Used <= memory.Total && Math.Abs(memory.Load!.Value - memory.Used!.Value / memory.Total!.Value * 100) < 0.01);
        var monitor = new HardwareMonitorService(1000);
        monitor.Configure(new AppSettings { HardwareSensorsEnabled = false, EcoMode = false, MonitorUpdateIntervalMs = 1000 }, true);
        using var sampled = new ManualResetEventSlim();
        var snapshots = new List<HardwareInfoSnapshot>();
        monitor.HardwareUpdated += value => { lock (snapshots) { snapshots.Add(value); if (snapshots.Count >= 3) sampled.Set(); } };
        monitor.Start();
        Check("Monitor publishes three frames", sampled.Wait(10000));
        lock (snapshots)
        {
            Check("Sequences monotonic", snapshots.Zip(snapshots.Skip(1), (a,b) => b.Sequence > a.Sequence).All(x => x));
            Check("Physical RAM total stable", snapshots.Count >= 3 && snapshots.Max(x => x.RamTotalGB) == snapshots.Min(x => x.RamTotalGB));
        Check("Disabled native sensors remain unavailable", snapshots.All(x => x.Hardware.CpuTemp == null));
        }
        var stopwatch = Stopwatch.StartNew(); monitor.Dispose(); stopwatch.Stop();
        Check("Dispose returns without UI wait", stopwatch.ElapsedMilliseconds < 100);
        monitor.Dispose(); monitor.Configure(new AppSettings(), true); monitor.Start();
        Check("Repeated teardown and late configuration do not throw", true);
        using (var cold = new HardwareMonitorService()) { cold.Dispose(); cold.Start(); }
        Check("Dispose before start is safe", true);
        TelemetryFreshnessTests.RunRecovery(Check);
        using (var broken = new HardwareMonitorService(() => throw new IOException("Injected provider failure"), 1000))
        {
            broken.Configure(new AppSettings { EcoMode = false, MonitorUpdateIntervalMs = 1000 }, true);
            using var healthyWindows = new ManualResetEventSlim();
            broken.HardwareUpdated += frame => { if (frame.Sequence >= 3 && frame.Hardware.Error != null && frame.RamTotalGB > 0) healthyWindows.Set(); };
            broken.Start();
            Check("Provider failure does not stop Windows metrics", healthyWindows.Wait(7000));
        }

        using (var cadence = new HardwareMonitorService())
        {
            cadence.Configure(new AppSettings { HardwareSensorsEnabled = false, EcoMode = false, MonitorUpdateIntervalMs = 10000 }, true);
            using var ready = new ManualResetEventSlim(); cadence.HardwareUpdated += _ => ready.Set(); cadence.Start();
            Check("Slow cadence initializes immediately", ready.Wait(5000));
            long beforeChange=cadence.WindowsSampleCount;
            cadence.Configure(new AppSettings { HardwareSensorsEnabled = false, EcoMode = false, MonitorUpdateIntervalMs = 1000 }, true);
            Check("Interval applies without restart",SpinWait.SpinUntil(()=>cadence.WindowsSampleCount>=beforeChange+2,3500));
            long beforeHidden=cadence.WindowsSampleCount;
            cadence.Configure(new AppSettings { HardwareSensorsEnabled = false, EcoMode = true, SuspendMonitorWhenHidden=false, MonitorUpdateIntervalMs = 1000 }, false);
            Check("Hidden configuration wakes sampler once",SpinWait.SpinUntil(()=>cadence.WindowsSampleCount>beforeHidden,3000));
            long hiddenCount=cadence.WindowsSampleCount;
            Thread.Sleep(1500);
            Check("Hidden eco cadence does not retain one-second polling",cadence.WindowsSampleCount==hiddenCount);
        }

        if (args.Contains("--native"))
        {
            var sensorExe = Array.IndexOf(args, "--sensor-exe");
            using var nativeMonitor = sensorExe >= 0
                ? new HardwareMonitorService(() => new Nepply.Services.Telemetry.SensorProcessProvider(args[sensorExe + 1]))
                : new HardwareMonitorService();
            using var nativeReady = new ManualResetEventSlim();
            HardwareInfoSnapshot nativeSample = HardwareInfoSnapshot.Empty;
            long lastNativeTimestamp = 0; int nativeFrames = 0;
            nativeMonitor.Configure(new AppSettings { EcoMode = false, MonitorUpdateIntervalMs = 1000 }, true);
            nativeMonitor.HardwareUpdated += sample => { if (sample.Hardware.MonotonicTimestamp != 0 && sample.Hardware.MonotonicTimestamp != lastNativeTimestamp) { nativeSample = sample; lastNativeTimestamp = sample.Hardware.MonotonicTimestamp; if (++nativeFrames >= 3) nativeReady.Set(); } };
            nativeMonitor.Start();
            Check("Native monitor publishes sensor sample", nativeReady.Wait(TimeSpan.FromSeconds(25)));
            Check("Native GPU identities unique", nativeSample.Hardware.Gpus.DistinctBy(g => g.Id).Count() == nativeSample.Hardware.Gpus.Length);
            Check("Native missing CPU temp not zero", nativeSample.Hardware.CpuTemp == null || nativeSample.Hardware.CpuTemp > 0);
            Check("Native all GPU selection retains inventory", nativeSample.Hardware.VisibleGpus.Length == nativeSample.Hardware.Gpus.Length);
            if (nativeSample.Hardware.CpuName.Contains("4600G"))
            {
                Check("4600G AMD fallback supplies CPU temperature", nativeSample.Hardware.CpuTemp is > 0 and < 130);
                Check("4600G AMD fallback supplies GPU temperature", nativeSample.Hardware.Gpus.Any(g => g.Temperature is > 0 and < 130));
                Check("4600G temperature source explicit", nativeSample.Hardware.CpuTelemetrySource.Contains("ADL"));
            }
            File.WriteAllText(Path.Combine(output, "native-sample.json"), System.Text.Json.JsonSerializer.Serialize(nativeSample, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        bool desktopIntegration = !args.Contains("--ui-only");
        IReadOnlyList<DesktopIcon> actualIcons = [new() {Name="Fixture A",Path="fixture-a"},new() {Name="Fixture B",Path="fixture-b"}];
        if (desktopIntegration)
        {
        using var desktopScan = new DesktopIconService();
        Check("Actual Shell scan completes", Task.Run(() => desktopScan.ScanDesktopIconsAsync()).Wait(TimeSpan.FromSeconds(30)) && desktopScan.HasScanned);
        actualIcons = desktopScan.GetIcons();
        var exeArg = Array.IndexOf(args,"--sensor-exe");
        if(exeArg >= 0)
        {
            using var isolated = new DesktopIconService(args[exeArg+1]);
            var requests = Enumerable.Range(0,30).Select(_=>isolated.ScanDesktopIconsAsync()).ToArray();
            Check("Desktop burst requests share one worker",requests.Distinct().Count()==1);
            Check("Isolated Shell scan completes",Task.WhenAll(requests).Wait(TimeSpan.FromSeconds(55)) && isolated.HasScanned);
            Check("Isolated Shell retains complete inventory",isolated.GetIcons().Select(i=>i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(actualIcons.Select(i=>i.Path)));
            Check("Isolated Shell transfers icon images",isolated.GetIcons().Any(i=>i.Icon != null));
        }
        bool controlPanelEnabled=ShellDesktopReader.IsDesktopSystemIcon("::{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}");
        Check("Control Panel follows Windows visibility and is not duplicated", actualIcons.Count(i => i.Path.StartsWith("::{26EE0668-", StringComparison.OrdinalIgnoreCase) || i.Path.StartsWith("::{5399E694-", StringComparison.OrdinalIgnoreCase)) == (controlPanelEnabled ? 1 : 0));
        Check("Actual desktop identities unique", ShellDesktopReader.RunSta(() => actualIcons.Select(ShellDesktopReader.Identity).Distinct(StringComparer.Ordinal).Count()).GetAwaiter().GetResult() == actualIcons.Count);
        var shellItems = ShellDesktopReader.RunSta(() => ShellDesktopReader.Read(false, true, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory))).GetAwaiter().GetResult();
        Check("Shell physical folders retained", shellItems.Where(i => Directory.Exists(i.Path)).All(i => actualIcons.Any(a => a.Path.Equals(i.Path, StringComparison.OrdinalIgnoreCase))));
        var desktopRoots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) };
        Check("No personal navigation folders included", actualIcons.Where(i => !i.IsSystemIcon).All(i => desktopRoots.Contains(Path.GetDirectoryName(i.Path),StringComparer.OrdinalIgnoreCase)));
        Check("Home namespace rejected", !ShellDesktopReader.IsDesktopSystemIcon("::{F874310E-B6B7-47DC-BC84-B9E6B38F5903}"));
        Check("Libraries namespace rejected", !ShellDesktopReader.IsDesktopSystemIcon("::{031E4825-7B94-4DC3-B131-E946B44C8DD5}"));
        Check("Unsupported AMD metric rejected", AmdTelemetry.ValidateMetric(0, 45, 1, 130) == null);
        Check("Invalid AMD temperature rejected", AmdTelemetry.ValidateMetric(1, 0, 1, 130) == null);
        Check("Supported AMD temperature retained", AmdTelemetry.ValidateMetric(1, 45, 1, 130) == 45);
        Check("HSV red converts to RGB", ColorField.FromHsv(0,1,1) == Colors.Red);
        Check("HSV hue wraps", ColorField.FromHsv(360,1,1) == Colors.Red);
        Check("HSV white and black endpoints", ColorField.FromHsv(100,0,1) == Colors.White && ColorField.FromHsv(100,1,0) == Colors.Black);
        var layoutDraft = new AppSettings(); var layoutClone = layoutDraft.Clone(); layoutClone.WidgetOrder[0] = "network";
        Check("Layout settings clone isolated", layoutDraft.WidgetOrder[0] == "summary");
        layoutClone.WidgetOrder = ["network", "invalid", "network"]; layoutClone.Validate();
        Check("Widget order normalized without retired details", layoutClone.WidgetOrder.SequenceEqual(new[] { "network", "summary", "gpus" }));
        foreach (var root in desktopRoots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                Check("Desktop integration requires an available user/public Desktop directory",false);
                continue;
            }
            Check("Every visible Desktop destination represented: " + root, Directory.EnumerateFileSystemEntries(root)
                .Where(p => (File.GetAttributes(p) & FileAttributes.Hidden) == 0 && !Path.GetFileName(p).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                .All(p => actualIcons.Any(i => i.Path.Equals(p,StringComparison.OrdinalIgnoreCase)) ||
                    ShellDesktopReader.RunSta(() => actualIcons.Any(i => ShellDesktopReader.DestinationIdentity(i) == ShellDesktopReader.DestinationIdentity(new DesktopIcon {Path=p,Name=Path.GetFileNameWithoutExtension(p)}))).GetAwaiter().GetResult()));
        }
        Check("Equivalent desktop shortcuts appear once",ShellDesktopReader.RunSta(() => actualIcons.Select(ShellDesktopReader.DestinationIdentity).Distinct().Count()==actualIcons.Count).GetAwaiter().GetResult());
        File.WriteAllLines(Path.Combine(output,"desktop-inventory.txt"), actualIcons.Select(i => i.Name + " | " + i.Path));
        }
        Check("Different shortcut paths are distinct", ShellDesktopReader.Identity(new DesktopIcon { Path = "C:\\User\\Chrome.lnk" }) != ShellDesktopReader.Identity(new DesktopIcon { Path = "C:\\Public\\Chrome.lnk" }));
        var gate = new EdgeActivationGate();
        Check("Edge dwell waits", !gate.Update(true,true,false,0,120));
        Check("Edge dwell opens once", gate.Update(true,true,false,120,120));
        Check("Stationary edge never reopens", !gate.Update(true,true,false,10000,120));
        Check("Release hysteresis prevents jitter", !gate.Update(false,true,false,11000,120) && !gate.Update(true,true,false,12000,0));
        gate.Update(false,false,false,13000,120);
        Check("Leaving edge rearms reveal", gate.Update(true,true,false,14000,0));
        Check("Animation cannot trigger reveal", !gate.Update(true,true,true,15000,0));
        Check("Auto hide cancels false leave while pointer remains inside",
            AutoHidePolicy.Decide(false,false,false,true)==AutoHideAction.Cancel);
        Check("Auto hide restarts full delay after animation",
            AutoHidePolicy.Decide(false,false,true,false)==AutoHideAction.RestartCountdown);
        Check("Pinned and suppressed panels cancel pending hide",
            AutoHidePolicy.Decide(true,false,false,false)==AutoHideAction.Cancel
            && AutoHidePolicy.Decide(false,true,false,false)==AutoHideAction.Cancel);
        Check("Stable pointer outside permits hide",
            AutoHidePolicy.Decide(false,false,false,false)==AutoHideAction.Hide);
        Check("Installed RAM at least OS usable", SystemMetrics.ReadInstalledMemory() >= memory.Total);
        var app = new App(testHost: true); app.InitializeComponent();
        BrandUiTests.Run(Check);
        UpdateUiTests.Run(output, Check, Pump);
        DesktopContractTests.Run(Check, Pump);
        MonitorSubscriptionTests.Run(Check,Pump);
        var keyboardWidgets=new System.Windows.Controls.StackPanel();
        var firstWidget=new System.Windows.Controls.Border();
        var hiddenWidget=new System.Windows.Controls.Border { Visibility=Visibility.Collapsed };
        var lastWidget=new System.Windows.Controls.Border();
        keyboardWidgets.Children.Add(firstWidget); keyboardWidgets.Children.Add(hiddenWidget); keyboardWidgets.Children.Add(lastWidget);
        int reorderEvents=0;
        keyboardWidgets.AddHandler(WidgetDrag.ReorderedEvent,new RoutedEventHandler((_,_)=>reorderEvents++));
        Check("Keyboard widget reorder disabled outside edit mode",!WidgetDrag.MoveByKeyboard(firstWidget,1));
        WidgetDrag.SetEnabled(firstWidget,true);
        Check("Editing widget becomes keyboard focusable",firstWidget.Focusable);
        Check("Keyboard widget reorder skips collapsed sections",WidgetDrag.MoveByKeyboard(firstWidget,1) && keyboardWidgets.Children.IndexOf(firstWidget)==2 && keyboardWidgets.Children.IndexOf(lastWidget)==1 && reorderEvents==1);
        Check("Keyboard widget reorder respects lower boundary",!WidgetDrag.MoveByKeyboard(firstWidget,1) && reorderEvents==1);
        Check("Keyboard widget reorder moves back upward",WidgetDrag.MoveByKeyboard(firstWidget,-1) && keyboardWidgets.Children.IndexOf(firstWidget)==1 && reorderEvents==2);
        WidgetDrag.SetEnabled(firstWidget,false);
        Check("Leaving edit restores original focusability",!firstWidget.Focusable && !WidgetDrag.MoveByKeyboard(firstWidget,-1));
        var focusableWidget=new System.Windows.Controls.Button();
        WidgetDrag.SetEnabled(focusableWidget,true); WidgetDrag.SetEnabled(focusableWidget,false);
        Check("Naturally focusable widget remains focusable",focusableWidget.Focusable);
        int scanAttempt=0;
        using (var faultScanner = new DesktopIconService(_ =>
        {
            int attempt=Interlocked.Increment(ref scanAttempt);
            return attempt is 1 or 3
                ? Task.FromException<List<DesktopIcon>>(new IOException("Injected scan failure"))
                : Task.FromResult(new List<DesktopIcon> { new() { Name="Retained file", Path="test-desktop-item" } });
        }))
        {
            var scanVm=new DesktopIconViewModel(); scanVm.Initialize(faultScanner);
            faultScanner.ScanDesktopIconsAsync().GetAwaiter().GetResult(); Pump(30);
            Check("Initial scan failure leaves loading state",!scanVm.IsLoading && scanVm.HasScanError && !scanVm.IsEmpty);
            faultScanner.ScanDesktopIconsAsync().GetAwaiter().GetResult(); Pump(30);
            Check("Retry clears scan error and publishes icons",!scanVm.HasScanError && scanVm.Icons.Count==1 && faultScanner.ScanState.HasSnapshot);
            var retained=scanVm.Icons[0];
            faultScanner.ScanDesktopIconsAsync().GetAwaiter().GetResult(); Pump(30);
            Check("Failed refresh preserves previous icon snapshot",scanVm.HasScanError && scanVm.Icons.Count==1 && ReferenceEquals(retained,scanVm.Icons[0]));
            faultScanner.ScanDesktopIconsAsync().GetAwaiter().GetResult(); Pump(30);
            Check("Refresh recovers after intermittent failure",!scanVm.HasScanError && !faultScanner.ScanState.IsScanning);
            scanVm.Cleanup();
        }
        using (var enteredScan=new ManualResetEventSlim())
        {
            var canceledScanner=new DesktopIconService(async token => { enteredScan.Set(); await Task.Delay(Timeout.Infinite,token); return new List<DesktopIcon>(); });
            int scanEvents=0; canceledScanner.ScanStateChanged+=()=>Interlocked.Increment(ref scanEvents);
            var pendingScan=canceledScanner.ScanDesktopIconsAsync();
            Check("Injected long scan starts",enteredScan.Wait(2000));
            canceledScanner.Dispose(); int eventsAtDispose=scanEvents;
            Check("Disposal cancels in-flight scan",pendingScan.Wait(2000));
            Check("Canceled scan emits no late status",scanEvents==eventsAtDispose);
        }
        using var bindingErrors = new StringWriter();
        using var bindingListener = new TextWriterTraceListener(bindingErrors);
        PresentationTraceSources.DataBindingSource.Listeners.Add(bindingListener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        Check("Shortcut case normalized", ShellDesktopReader.ShortcutIdentity("Chrome", "C:\\CHROME.exe", "", "C:\\") == ShellDesktopReader.ShortcutIdentity("chrome", "c:\\chrome.exe", "", "c:\\"));
        Check("Distinct shortcut arguments preserved", ShellDesktopReader.ShortcutIdentity("Chrome", "C:\\chrome.exe", "--profile A", "") != ShellDesktopReader.ShortcutIdentity("Chrome", "C:\\chrome.exe", "--profile B", ""));
        Check("Argument case preserved", ShellDesktopReader.ShortcutIdentity("Tool", "C:\\tool.exe", "TokenA", "") != ShellDesktopReader.ShortcutIdentity("Tool", "C:\\tool.exe", "tokena", ""));
        Check("Same name different file preserved", ShellDesktopReader.Identity(new DesktopIcon { Name = "Notes", Path = "C:\\a\\notes.txt" }) != ShellDesktopReader.Identity(new DesktopIcon { Name = "Notes", Path = "C:\\b\\notes.txt" }));
        Check("Control Panel aliases merged", ShellDesktopReader.Identity(new DesktopIcon { Path = "::{26EE0668-A00A-44D7-9371-BEB064C98683}" }) == ShellDesktopReader.Identity(new DesktopIcon { Path = "::{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}" }));
        var adapterA = new GpuReading("discrete", "Dedicated sample", 70, 65, 1800, "test");
        var adapterB = new GpuReading("integrated", "Integrated sample", 12, null, 400, "test") { Label = "iGPU" };
        ImmutableArray<GpuReading> adapters = [adapterA, adapterB];
        var gpuSettings = new AppSettings { GpuIdentifier = "discrete", IntegratedGpuIdentifier = "integrated" };
        Check("Both GPUs retain independent data", HardwareMonitorService.SelectGpus(adapters, gpuSettings) is { Length: 2 } both && both[0].Temperature == 65 && both[1].Temperature == null);
        gpuSettings.GpuDisplay = GpuDisplayMode.Secondary;
        Check("Integrated selection uses identifier", HardwareMonitorService.SelectGpus(adapters, gpuSettings) is { Length: 1 } integrated && integrated[0].Id == "integrated" && integrated[0].Label == "iGPU");
        gpuSettings.GpuDisplay = GpuDisplayMode.Primary;
        Check("Discrete selection uses identifier", HardwareMonitorService.SelectGpus(adapters, gpuSettings) is { Length: 1 } discrete && discrete[0].Id == "discrete");
        Check("Missing selected GPU never substitutes", HardwareMonitorService.SelectGpus([adapterB], gpuSettings).Length == 0);
        Check("Missing GPU temperature not zero", adapterB.TemperatureText == "");
        var paletteSettings = new AppSettings { Accent = "Custom", CustomAccent = "invalid" }; paletteSettings.Validate();
        Check("Invalid custom color normalized", paletteSettings.CustomAccent == "#D9BC8B");
        Check("Hex colors validated", AppSettings.IsHexColor("#39E7F5") && !AppSettings.IsHexColor("#XYZXYZ") && !AppSettings.IsHexColor(null));
        foreach (var palette in ThemeService.Palettes.Keys) ThemeService.Apply(new AppSettings { Accent = palette });
        Check("Every metallic neon palette applies", true);
        ButtonAppearanceTests.Run(Check, Pump);
        InputAppearanceTests.Run(Check, Pump, output, Render);
        SwatchAppearanceTests.Run(Check, Pump, output, Render);
        StateAppearanceTests.Run(Check, Pump, output, Render);
        MaterialAppearanceTests.Run(Check, Pump, output, Render);
        MotionPolicyTests.Run(Check,Pump);
        foreach(var theme in new[] { ThemeOption.Dark, ThemeOption.Light })
        foreach(var custom in new[] { "#000000", "#FFFFFF", "#001020", "#FFFFAA" })
        {
            var customSettings = new AppSettings { Theme = theme, Accent = "Custom", CustomAccent = custom };
            ThemeService.ApplyAccessibility(customSettings,false,true);
            var chosen = (Color)ColorConverter.ConvertFromString(custom);
            var functional = ((SolidColorBrush)app.Resources["AccentBrush"]).Color;
            var source = ((SolidColorBrush)app.Resources["AccentSourceBrush"]).Color;
            var onAccent = ((SolidColorBrush)app.Resources["OnAccentBrush"]).Color;
            Check($"Theme {theme}/{custom} preserves source color and saved preference",source == chosen && customSettings.CustomAccent == custom);
            Check($"Theme {theme}/{custom} publishes readable functional accent",
                new[] { "SurfaceBrush", "CardBrush", "HoverBrush" }.All(key => ThemeService.Contrast(functional,((SolidColorBrush)app.Resources[key]).Color) >= 4.5)
                && ThemeService.Contrast(functional,onAccent) >= 4.5);
        }
        var accessibilitySettings=new AppSettings { DynamicMaterial=true, Accent="Neon Cyan" };
        ThemeService.ApplyAccessibility(accessibilitySettings,true,true);
        var contrastCard=new System.Windows.Controls.Border();
        MetallicSurface.SetAccent(contrastCard,Brushes.Cyan);
        Check("High contrast uses system text and surface colors",ReferenceEquals(app.Resources["TextBrush"],SystemColors.WindowTextBrush) && ReferenceEquals(app.Resources["SurfaceBrush"],SystemColors.WindowBrush));
        Check("High contrast removes decorative effects",contrastCard.Effect==null && ReferenceEquals(contrastCard.Background,SystemColors.WindowBrush) && contrastCard.BorderThickness.Left==1 && !MetallicSurface.MotionEnabled);
        Check("High contrast scrollbar stays opaque",(double)app.Resources["ScrollRailOpacity"]==1);
        Check("High contrast progress distinguishes filled and remaining areas",!Equals(((SolidColorBrush)app.Resources["ProgressTrackBrush"]).Color,((SolidColorBrush)app.Resources["AccentGradientBrush"]).Color) && ReferenceEquals(app.Resources["ProgressOutlineBrush"],SystemColors.WindowTextBrush));
        ThemeService.ApplyAccessibility(accessibilitySettings,false,false);
        Check("Windows animation preference stops metallic motion",!MetallicSurface.MotionEnabled && !MetallicSurface.HighContrast);
        ThemeService.ApplyAccessibility(accessibilitySettings,false,true);
        Check("Normal theme restores configured motion",MetallicSurface.MotionEnabled && (double)app.Resources["ScrollRailOpacity"]==.35);
        Check("Reduce motion setting removed", typeof(AppSettings).GetProperty("ReduceMotion") == null);
        var preview = new MainWindow(true); preview.ApplySettings();
        var pinToggle=(System.Windows.Controls.Primitives.ToggleButton)preview.FindName("PinButton");
        var pinPeer=new System.Windows.Automation.Peers.ToggleButtonAutomationPeer(pinToggle);
        var pinProvider=(System.Windows.Automation.Provider.IToggleProvider)pinPeer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Toggle)!;
        Check("Pin exposes native TogglePattern initially off",pinProvider.ToggleState==System.Windows.Automation.ToggleState.Off && !preview.IsPinned);
        pinProvider.Toggle();
        Check("Pin automation updates panel policy and checked state",preview.IsPinned && pinToggle.IsChecked==true && pinProvider.ToggleState==System.Windows.Automation.ToggleState.On);
        preview.TogglePin();
        Check("Pin external command synchronizes without double toggle",!preview.IsPinned && pinToggle.IsChecked==false);
        pinToggle.IsChecked=true;
        Check("Pin checked change follows the same panel policy",preview.IsPinned);
        preview.HidePanel();
        Check("Hide panel clears native toggle state",!preview.IsPinned && pinProvider.ToggleState==System.Windows.Automation.ToggleState.Off);
        var shellIcons=(IconPanel)preview.FindName("IconPanelView");
        var desktopSearchEditor=(FrameworkElement)preview.FindName("DesktopSearchEditor");
        var desktopSearchVm=(DesktopIconViewModel)shellIcons.DataContext;
        Pump(30);
        Check("Panel search starts collapsed to an icon",desktopSearchEditor.Visibility==Visibility.Collapsed);
        desktopSearchVm.SearchQuery="test"; Pump(30);
        Check("Panel search stays expanded while a filter exists",desktopSearchEditor.Visibility==Visibility.Visible);
        // Collapse is a 240 ms dismiss delay plus the reveal animation; wait for it, bounded.
        desktopSearchVm.SearchQuery="";
        Check("Empty inactive panel search collapses",PumpUntil(()=>desktopSearchEditor.Visibility==Visibility.Collapsed,2000));
        desktopSearchVm.SearchQuery="test";
        ((FrameworkElement)preview.FindName("MainPanel")).RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.Input.Mouse.PreviewMouseDownEvent});
        Pump(250);
        Check("Blank panel click clears typed filter and collapses search",desktopSearchVm.SearchQuery=="" && desktopSearchEditor.Visibility==Visibility.Collapsed);
        var shellHotkey=(System.Windows.Controls.TextBlock)preview.FindName("HotkeyText");
        preview.PresentHotkeyStatus(false);
        Check("Unavailable hotkey has visible actionable feedback",
            shellHotkey.Text.Contains("chưa khả dụng") && ((FrameworkElement)shellIcons.FindName("LaunchNotice")).Visibility==Visibility.Visible
            && ((System.Windows.Controls.TextBlock)shellIcons.FindName("LaunchNoticeText")).Text.Contains("khay hệ thống"));
        preview.PresentHotkeyStatus(true);
        Check("Recovered hotkey dismisses its stale error",!shellHotkey.Text.Contains("chưa khả dụng")
            && ((FrameworkElement)shellIcons.FindName("LaunchNotice")).Visibility==Visibility.Collapsed);
        preview.PresentHotkeyStatus(false);
        shellIcons.ShowActionNotice("Unrelated operation failure");
        preview.PresentHotkeyStatus(true);
        Check("Recovered hotkey preserves unrelated operation notice",((FrameworkElement)shellIcons.FindName("LaunchNotice")).Visibility==Visibility.Visible);
        shellIcons.ShowActionNotice(null);
        var exitSettings=new SettingsWindow(new AppSettings());
        var exitDraftWidth=(System.Windows.Controls.Slider)exitSettings.FindName("OpacitySlider");
        exitDraftWidth.Value=91;
        var settingsField=typeof(MainWindow).GetField("_settingsWindow",BindingFlags.Instance | BindingFlags.NonPublic)!;
        settingsField.SetValue(preview,exitSettings);
        try
        {
            Check("Tray exit preserves open settings and its draft",!preview.ConfirmExit() && exitDraftWidth.Value==91
                && ((System.Windows.Controls.TextBlock)exitSettings.FindName("ErrorText")).Text.Contains("trước khi thoát"));
        }
        finally { settingsField.SetValue(preview,null); exitSettings.Close(); }
        Check("Tray exit is available without open editor",preview.ConfirmExit());
        var monitorPanel = (MonitorPanel)preview.FindName("MonitorPanelView");
        var vm = (HardwareMonitorViewModel)monitorPanel.DataContext;
        var demo = new HardwareInfoSnapshot(1, DateTimeOffset.UtcNow, 24, 8.4, 16, 52.5f, 98304, 2516582, "Wi-Fi",
            new(DateTimeOffset.UtcNow, "CPU · minh họa giao diện", "GPU · minh họa giao diện", 48, 3600, 16, 42, 1200, 1600, null), []);
        typeof(HardwareMonitorViewModel).GetField("_display", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, demo);
        vm.UpdateGpus(HardwareMonitorService.SelectGpus(adapters, new AppSettings { GpuIdentifier = "discrete", IntegratedGpuIdentifier = "integrated" }));
        var firstCard = vm.Gpus[0]; vm.UpdateGpus([adapterA with { Load = 75 }, adapterB]);
        Check("GPU card reused between samples", ReferenceEquals(firstCard, vm.Gpus[0]) && vm.Gpus[0].LoadValue == 75);
        monitorPanel.DataContext = null; monitorPanel.DataContext = vm;
        var icons = (IconPanel)preview.FindName("IconPanelView");
        Check("Desktop empty state has no redundant clear-filter action",
            !((System.Windows.Controls.StackPanel)icons.FindName("EmptyState")).Children.OfType<System.Windows.Controls.Button>().Any());
        var secondaryDisplay=new Nepply.Helpers.DisplayScreen("secondary",-1920,0,0,1080,1,false);
        var primaryDisplay=new Nepply.Helpers.DisplayScreen("primary",0,0,1920,1080,1.5,true);
        Check("Display selection respects device before primary fallback",
            Nepply.Helpers.ScreenManager.SelectAvailable([primaryDisplay,secondaryDisplay],"SECONDARY")==secondaryDisplay);
        Check("Display selection handles missing primary flag",
            Nepply.Helpers.ScreenManager.SelectAvailable([secondaryDisplay],"disconnected")==secondaryDisplay);
        Check("Empty or invalid display topology has no fabricated geometry",
            Nepply.Helpers.ScreenManager.SelectAvailable([],"")==null
            && Nepply.Helpers.ScreenManager.SelectAvailable([primaryDisplay with {Scale=0}],"")==null);
        Check("Display selection falls back to primary when saved screen is absent",
            Nepply.Helpers.ScreenManager.SelectAvailable([secondaryDisplay,primaryDisplay],"missing")==primaryDisplay);
        var displayChoices=SettingsWindow.BuildScreenChoices(
            [new Nepply.Helpers.DisplayScreen("DISPLAY1",0,0,1920,1080,1,true)],"DISPLAY2");
        Check("Display settings retain disconnected selection",displayChoices.Any(c=>c.Id=="DISPLAY2" && c.Name.Contains("chưa kết nối")));
        Check("Display settings offer explicit follow-primary option",displayChoices[0].Id=="" && displayChoices[0].Name=="Theo màn hình chính");
        var caseChoices=SettingsWindow.BuildScreenChoices(
            [new Nepply.Helpers.DisplayScreen("DISPLAY1",0,0,1920,1080,1,true)],"display1");
        Check("Display settings preserve selected ID casing without duplicate screen",caseChoices.Length==2 && caseChoices[1].Id=="display1" && !caseChoices[1].Name.Contains("chưa kết nối"));
        Check("Keyboard interaction keeps dock open with pointer outside",
            AutoHidePolicy.Decide(false,false,false,false,true)==AutoHideAction.Cancel);
        Check("Ending keyboard interaction allows normal auto-hide",
            AutoHidePolicy.Decide(false,false,false,false,false)==AutoHideAction.Hide);
        Check("Keyboard interaction remains protected during reveal animation",
            AutoHidePolicy.Decide(false,false,true,false,true)==AutoHideAction.Cancel);
        var boundColorField=new ColorField();
        var colorSource=new System.Windows.Controls.Border {Tag=Colors.Coral};
        System.Windows.Data.BindingOperations.SetBinding(boundColorField,ColorField.SelectedColorProperty,
            new System.Windows.Data.Binding("Tag") {Source=colorSource,Mode=System.Windows.Data.BindingMode.TwoWay});
        typeof(ColorField).GetMethod("Commit",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(boundColorField,null);
        Check("Color interaction preserves its selected-color binding",
            System.Windows.Data.BindingOperations.IsDataBound(boundColorField,ColorField.SelectedColorProperty));
        colorSource.Tag=Colors.Gold; Pump(30);
        Check("Bound color still accepts external changes after interaction",boundColorField.SelectedColor==Colors.Gold);
        System.Windows.Automation.AutomationProperties.SetName(boundColorField,"Bảng màu RGB");
        var colorPeer=System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(boundColorField)!;
        var colorValue=(System.Windows.Automation.Provider.IValueProvider)colorPeer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Value)!;
        Check("Color picker exposes its accessible name and RGB value",colorPeer.GetName()=="Bảng màu RGB" && colorValue.Value=="#FFD700" && !colorValue.IsReadOnly);
        colorValue.SetValue("#39e7f5"); Pump(30);
        Check("Accessible color edit normalizes HEX and preserves binding",colorValue.Value=="#39E7F5"
            && System.Windows.Data.BindingOperations.IsDataBound(boundColorField,ColorField.SelectedColorProperty));
        Check("Accessible color edit updates bound source",colorSource.Tag is Color updatedColor && updatedColor==boundColorField.SelectedColor);
        bool invalidColorRejected=false;
        try {colorValue.SetValue("not-a-color");} catch(ArgumentException) {invalidColorRejected=true;}
        Check("Invalid accessible color preserves previous selection",invalidColorRejected && colorValue.Value=="#39E7F5");
        boundColorField.IsEnabled=false;
        bool disabledColorRejected=false;
        try {colorValue.SetValue("#FFFFFF");} catch(System.Windows.Automation.ElementNotEnabledException) {disabledColorRejected=true;}
        Check("Disabled color picker rejects accessible edits",disabledColorRejected && colorValue.Value=="#39E7F5");
        boundColorField.IsEnabled=true;
        colorSource.Tag=Colors.Coral; Pump(30);
        Check("Accessible color value follows external updates",colorValue.Value=="#FF7F50");
        var layoutBaseline=new AppSettings {IconOrder=[@"C:\Desktop\A.lnk",@"C:\Desktop\B.lnk"]};
        var layoutComparison=layoutBaseline.Clone();
        Check("Cloned layout is unchanged despite cloned arrays",LayoutProfile.SameLayout(layoutBaseline,layoutComparison));
        layoutComparison.GridCellWidth+=8;
        Check("Layout comparison detects grid edit",!LayoutProfile.SameLayout(layoutBaseline,layoutComparison));
        layoutComparison.GridCellWidth=layoutBaseline.GridCellWidth;
        Check("Returning grid to original value clears layout difference",LayoutProfile.SameLayout(layoutBaseline,layoutComparison));
        Array.Reverse(layoutComparison.IconOrder);
        Check("Layout comparison detects icon reorder",!LayoutProfile.SameLayout(layoutBaseline,layoutComparison));
        layoutComparison=layoutBaseline.Clone(); layoutComparison.IconOrder=layoutComparison.IconOrder.Select(p=>p.ToUpperInvariant()).ToArray();
        Check("Layout comparison ignores path casing",LayoutProfile.SameLayout(layoutBaseline,layoutComparison));
        string activationFixtureName=@"Local\Nepply-test-activate-"+Guid.NewGuid().ToString("N");
        using(var activationOwner=new EventWaitHandle(false,EventResetMode.AutoReset,activationFixtureName))
        {
            using(var secondLaunch=new EventWaitHandle(false,EventResetMode.AutoReset,activationFixtureName)) secondLaunch.Set();
            Check("Activation signal survives sender exit before owner waits",activationOwner.WaitOne(0));
            Check("Activation signal is consumed once",!activationOwner.WaitOne(0));
        }
        using(var instanceMutex=new Mutex(false))
        {
            bool acquired=Nepply.Helpers.InstanceOwnership.TryAcquire(instanceMutex);
            Check("Instance ownership acquires an unowned existing mutex",acquired);
            bool competingOwner=true;
            var competitor=new Thread(()=>
            {
                competingOwner=Nepply.Helpers.InstanceOwnership.TryAcquire(instanceMutex);
                if(competingOwner) instanceMutex.ReleaseMutex();
            });
            competitor.IsBackground=true; competitor.Start();
            bool competitorFinished=competitor.Join(2000);
            if(acquired) instanceMutex.ReleaseMutex();
            Check("Instance ownership rejects another active thread without waiting",competitorFinished && !competingOwner);
        }
        using(var abandonedInstance=new Mutex(false))
        {
            var previousOwner=new Thread(()=>abandonedInstance.WaitOne());
            previousOwner.IsBackground=true; previousOwner.Start();
            bool ownerExited=previousOwner.Join(2000);
            bool recovered=ownerExited && Nepply.Helpers.InstanceOwnership.TryAcquire(abandonedInstance);
            Check("Instance ownership recovers abandoned mutex",recovered);
            if(recovered) abandonedInstance.ReleaseMutex();
        }
        var queuedDebounce=new BoundedDebounce();
        Check("Idle debounce ignores unsolicited timer callback",queuedDebounce.Remaining(0)==null);
        queuedDebounce.Signal(0); queuedDebounce.Signal(300);
        Check("Queued old callback observes extended deadline",queuedDebounce.Remaining(350)==300);
        Check("Debounce becomes due at latest deadline",queuedDebounce.Remaining(650)==0);
        queuedDebounce.Reset();
        Check("Completed debounce ignores duplicate callback",queuedDebounce.Remaining(700)==null);
        queuedDebounce.Signal(1000); queuedDebounce.Signal(2900);
        Check("Repeated signals retain maximum wait",queuedDebounce.Remaining(3000)==0);
        Check("Color editor matches custom swatch even when preset is active",SettingsWindow.EditingColor("Neon Cyan","#FF0000")==Colors.Red);
        Check("Color editor retains custom color",SettingsWindow.EditingColor("Custom","#FF0000")==Colors.Red);
        Check("Color editor has safe fallback for incomplete HEX",SettingsWindow.EditingColor("Custom","#12")==Color.FromRgb(217,188,139));
        var cleanupOrder=new List<int>();
        Nepply.Helpers.CleanupSequence.Run(
            ("fixture first",()=>cleanupOrder.Add(1)),
            ("fixture failure",()=>throw new InvalidOperationException("Expected cleanup fixture failure")),
            ("fixture restore",()=>cleanupOrder.Add(3)));
        Check("Cleanup continues in order after a failing step",cleanupOrder.SequenceEqual(new[]{1,3}));
        Check("Dialog fitting preserves normal dimensions",Nepply.Helpers.DialogPlacement.FitSize(new Size(620,740),new Size(1920,1040))==new Size(620,740));
        Check("Dialog fitting keeps actions inside short work area",Nepply.Helpers.DialogPlacement.FitSize(new Size(620,740),new Size(1024,680))==new Size(620,656));
        Check("Dialog fitting uses logical high-DPI work area",Nepply.Helpers.DialogPlacement.FitSize(new Size(620,740),new Size(960,520))==new Size(620,496));
        Check("Dialog fitting handles narrow work area",Nepply.Helpers.DialogPlacement.FitSize(new Size(620,740),new Size(500,680))==new Size(476,656));
        bool accentTextContrast=true;
        for(int r=0;r<=255;r+=17)
            for(int g=0;g<=255;g+=17)
                for(int b=0;b<=255;b+=17)
                {
                    var background=Color.FromRgb((byte)r,(byte)g,(byte)b);
                    accentTextContrast &= ThemeService.Contrast(background,ThemeService.TextOnAccent(background))>=4.5;
                }
        Check("Accent text meets 4.5 contrast on sampled opaque RGB colors",accentTextContrast);
        Check("Contrast reference black against white is 21",Math.Abs(ThemeService.Contrast(Colors.Black,Colors.White)-21)<.0001);
        Check("Error text meets contrast on light and dark surfaces",
            ThemeService.Contrast((Color)ColorConverter.ConvertFromString("#A52F29"),(Color)ColorConverter.ConvertFromString("#F5F3EF"))>=4.5
            && ThemeService.Contrast((Color)ColorConverter.ConvertFromString("#ED8D80"),(Color)ColorConverter.ConvertFromString("#141619"))>=4.5);
        using(var invalidHotkey=new GlobalHotkeyService(IntPtr.Zero,()=>throw new Exception("Unexpected callback")))
        {
            Check("Hotkey without window is rejected without native registration",!invalidHotkey.Register());
            invalidHotkey.Dispose(); invalidHotkey.Dispose();
            bool disposedRejected=false;
            try { invalidHotkey.Register(); } catch(ObjectDisposedException) {disposedRejected=true;}
            Check("Disposed hotkey cannot register again",disposedRejected);
        }
        using(var profileStream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes(new LayoutProfile {CellWidth=112}.ToJson())))
        {
            Check("Layout stream parses without closing caller stream",LayoutProfile.Parse(profileStream).CellWidth==112 && profileStream.CanRead);
        }
        bool oversizedLayoutRejected=false;
        try { using var oversizedStream=new MemoryStream(new byte[65537]); LayoutProfile.Parse(oversizedStream); }
        catch(InvalidDataException) { oversizedLayoutRejected=true; }
        Check("Layout stream enforces byte limit",oversizedLayoutRejected);
        bool unicodeLayoutRejected=false;
        try { LayoutProfile.Parse("{\"Version\":1,\"Accent\":\""+new string('界',22000)+"\"}"); }
        catch(InvalidDataException) { unicodeLayoutRejected=true; }
        Check("Layout string enforces UTF8 byte limit",unicodeLayoutRejected);
        Check("Oversized icon row stays top-aligned on repeated reveal",
            IconGridPanel.OffsetToReveal(180,100,0,60)==180
            && IconGridPanel.OffsetToReveal(180,100,180,60)==180);
        Check("Visible icon row does not change scroll position",
            IconGridPanel.OffsetToReveal(100,90,80,240)==80);
        Check("Icon reveal scrolls only enough for ordinary rows",
            IconGridPanel.OffsetToReveal(20,90,80,240)==20
            && IconGridPanel.OffsetToReveal(300,90,80,240)==150);
        Check("Collapsed viewport does not move icon scroll offset",
            IconGridPanel.OffsetToReveal(180,100,80,0)==80);
        Check("Launch error distinguishes missing file and missing association",
            DesktopIconService.LaunchFailureKey(new System.ComponentModel.Win32Exception(2))=="DesktopLaunchMissing"
            && DesktopIconService.LaunchFailureKey(new System.ComponentModel.Win32Exception(1155))=="DesktopLaunchAssociation");
        Check("Launch error avoids leaking raw exception details into UI",
            DesktopIconService.LaunchFailureKey(new Exception("private diagnostic data"))=="DesktopLaunchError"
            && DesktopIconService.LaunchFailureKey(new UnauthorizedAccessException())=="DesktopLaunchDenied");
        using(var desktopSessionA=new DesktopIconService(_=>Task.FromResult(new List<DesktopIcon>())))
        using(var desktopSessionB=new DesktopIconService(_=>Task.FromResult(new List<DesktopIcon>())))
        {
            var sessionVm=new DesktopIconViewModel();
            sessionVm.Initialize(desktopSessionA);
            var desktopHandlerField=typeof(DesktopIconService).GetField("IconsChanged",BindingFlags.Instance|BindingFlags.NonPublic)!;
            var oldDesktopHandler=(Action?)desktopHandlerField.GetValue(desktopSessionA);
            sessionVm.Initialize(desktopSessionB);
            int desktopUpdates=0;
            sessionVm.PropertyChanged+=(_,e)=> { if(e.PropertyName==nameof(DesktopIconViewModel.CountText)) desktopUpdates++; };
            oldDesktopHandler?.Invoke(); Pump(60);
            Check("Old Desktop callbacks cannot duplicate new session updates",desktopUpdates==1);
            var scanStateMethod=typeof(DesktopIconService).GetMethod("SetScanState",BindingFlags.Instance|BindingFlags.NonPublic)!;
            scanStateMethod.Invoke(desktopSessionB,[new DesktopScanState(true,true)]); Pump(30);
            Check("Desktop refresh exposes busy state without first-load overlay",
                sessionVm.IsScanning && !sessionVm.CanRefresh && !sessionVm.IsLoading && sessionVm.ScanCaption=="Đang cập nhật…");
            scanStateMethod.Invoke(desktopSessionB,[new DesktopScanState(false,true,"fixture scan failure")]); Pump(30);
            Check("Desktop scan failure restores retry availability",
                !sessionVm.IsScanning && sessionVm.CanRefresh && sessionVm.HasScanError);
            Check("State-only Desktop updates do not re-filter the grid",desktopUpdates==1);
            var lateDesktopHandler=(Action?)desktopHandlerField.GetValue(desktopSessionB);
            scanStateMethod.Invoke(desktopSessionB,[new DesktopScanState(true,true)]);
            lateDesktopHandler?.Invoke(); lateDesktopHandler?.Invoke(); Pump(30);
            Check("Queued scan state preserves and coalesces icon updates",desktopUpdates==2);
            sessionVm.Icons.Add(new DesktopIcon {Name="Fixture",Path="fixture"});
            sessionVm.SelectedIcon=sessionVm.Icons[0]; sessionVm.Cleanup();
            desktopUpdates=0; lateDesktopHandler?.Invoke(); Pump(60);
            Check("Desktop cleanup clears retained icons and ignores late callbacks",
                sessionVm.Icons.Count==0 && sessionVm.SelectedIcon==null && desktopUpdates==0);
            Check("Desktop cleanup detaches handlers",desktopHandlerField.GetValue(desktopSessionA)==null
                && desktopHandlerField.GetValue(desktopSessionB)==null);
            var stateHandlerField=typeof(DesktopIconService).GetField("ScanStateChanged",BindingFlags.Instance|BindingFlags.NonPublic)!;
            Check("Desktop cleanup detaches state-only handlers",stateHandlerField.GetValue(desktopSessionA)==null
                && stateHandlerField.GetValue(desktopSessionB)==null);
        }
        var gridFixture=new IconPanel();
        gridFixture.ConfigureLayout(new AppSettings {GridColumns=6,GridRows=4},true);
        Check("Side grid caps requested columns at five",gridFixture.GridColumns==5 && gridFixture.GridRows==4);
        Check("Icon panel defaults to medium icons",gridFixture.IconPixels==36);
        gridFixture.ConfigureLayout(new AppSettings {IconSize=IconSizeOption.Large},false);
        bool largeIcons=gridFixture.IconPixels==44;
        gridFixture.ConfigureLayout(new AppSettings {IconSize=IconSizeOption.Small},false);
        Check("Icon panel follows the configured icon size",largeIcons && gridFixture.IconPixels==28);
        var sizedWindow=new MainWindow(true);
        sizedWindow.Settings.IconSize=IconSizeOption.Large; sizedWindow.ApplySettings();
        Check("Panel settings apply the configured icon size",((IconPanel)sizedWindow.FindName("IconPanelView")).IconPixels==44);
        sizedWindow.Cleanup(); sizedWindow.Close();
        Check("Grid editor sliders are removed from panel",gridFixture.FindName("GridWidth")==null && gridFixture.FindName("GridHeight")==null);
        Check("Icon context menu supports standard keyboard gestures",
            IconPanel.IsContextMenuKey(System.Windows.Input.Key.Apps,System.Windows.Input.ModifierKeys.None)
            && IconPanel.IsContextMenuKey(System.Windows.Input.Key.F10,System.Windows.Input.ModifierKeys.Shift));
        Check("Icon context menu leaves unrelated shortcuts untouched",
            !IconPanel.IsContextMenuKey(System.Windows.Input.Key.F10,System.Windows.Input.ModifierKeys.None)
            && !IconPanel.IsContextMenuKey(System.Windows.Input.Key.F10,System.Windows.Input.ModifierKeys.Control)
            && !IconPanel.IsContextMenuKey(System.Windows.Input.Key.Apps,System.Windows.Input.ModifierKeys.Alt));
        var scrollPace=new DragScrollPace();
        Check("Drag scroll sleeps at top boundary",!DragScrollPace.CanScroll(20,0,200,300,0,900));
        Check("Drag scroll sleeps at bottom boundary",!DragScrollPace.CanScroll(20,300,200,300,600,900));
        Check("Drag scroll skips lists fitting the viewport",!DragScrollPace.CanScroll(20,300,200,300,0,200));
        Check("Drag scroll allows both available directions",DragScrollPace.CanScroll(20,0,200,300,50,900)
            && DragScrollPace.CanScroll(20,300,200,300,50,900));
        Check("Drag scroll rejects stale horizontal geometry",!DragScrollPace.CanScroll(201,300,200,300,0,900)
            && !DragScrollPace.CanScroll(double.NaN,300,200,300,0,900));
        Check("Drag scroll only activates inside viewport edges",DragScrollPace.DirectionAt(0,300)==-1
            && DragScrollPace.DirectionAt(300,300)==1 && DragScrollPace.DirectionAt(150,300)==0
            && DragScrollPace.DirectionAt(-1,300)==0 && DragScrollPace.DirectionAt(301,300)==0);
        Check("Drag scroll ignores invalid geometry",DragScrollPace.DirectionAt(double.NaN,300)==0
            && DragScrollPace.DirectionAt(1,double.PositiveInfinity)==0 && DragScrollPace.DirectionAt(0,0)==0);
        Check("Drag scroll starts without a jump",scrollPace.Step(300,300,1000)==0);
        Check("Drag scroll follows elapsed time",Math.Abs(scrollPace.Step(300,300,1020)-6)<.001);
        Check("Duplicate drag event cannot accelerate scroll",scrollPace.Step(300,300,1020)==0);
        Check("Delayed drag event is capped",Math.Abs(scrollPace.Step(300,300,5000)-15)<.001);
        Check("Changing scroll direction starts a fresh interval",scrollPace.Step(0,300,5020)==0
            && Math.Abs(scrollPace.Step(0,300,5040)+6)<.001);
        Check("Leaving edge resets drag scrolling",scrollPace.Step(150,300,5060)==0
            && scrollPace.Step(300,300,5080)==0);
        var dragLifecyclePanel=new IconPanel();
        var dragTimer=(System.Windows.Threading.DispatcherTimer)typeof(IconPanel).GetField("_dragScrollTimer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dragLifecyclePanel)!;
        dragTimer.Start(); dragLifecyclePanel.SetEditMode(false);
        Check("Leaving icon editor stops drag-scroll timer",!dragTimer.IsEnabled);
        dragTimer.Start(); dragLifecyclePanel.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Check("Unloading icon panel stops drag-scroll timer",!dragTimer.IsEnabled);
        dragTimer.Start(); dragLifecyclePanel.IsEnabled=false;
        Check("Disabling icon panel stops drag-scroll timer",!dragTimer.IsEnabled);
        dragTimer.Start(); dragLifecyclePanel.Cleanup();
        Check("Icon cleanup stops drag-scroll timer",!dragTimer.IsEnabled);
        var selectionVm=new DesktopIconViewModel();
        var sameNameA=new DesktopIcon {Name="Browser",Path=@"C:\User\Browser.lnk"};
        var sameNameB=new DesktopIcon {Name="Browser",Path=@"C:\Public\Browser.lnk"};
        var allIconsField=typeof(DesktopIconViewModel).GetField("_all",BindingFlags.Instance|BindingFlags.NonPublic)!;
        allIconsField.SetValue(selectionVm,new[]{sameNameA,sameNameB});
        selectionVm.RefreshOrder(); selectionVm.SelectedIcon=sameNameB;
        var selectionSettings=new AppSettings {IconOrder=[sameNameA.Path,sameNameA.Path.ToUpperInvariant(),sameNameB.Path]};
        typeof(DesktopIconViewModel).GetField("_settings",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(selectionVm,selectionSettings);
        selectionVm.RefreshOrder();
        var unchangedOrder=selectionSettings.IconOrder;
        Check("Drop preview rejects foreign source and unchanged position",
            !selectionVm.CanReorder("foreign",sameNameA.Path)
            && !selectionVm.CanReorder(sameNameA.Path,sameNameB.Path));
        Check("Drop preview accepts valid move without mutating settings",
            selectionVm.CanReorder(sameNameB.Path,sameNameA.Path)
            && ReferenceEquals(unchangedOrder,selectionSettings.IconOrder));
        Check("Adjacent no-op drop leaves settings untouched",
            !selectionVm.Reorder(sameNameA.Path,sameNameB.Path)
            && ReferenceEquals(unchangedOrder,selectionSettings.IconOrder));
        var reorderActions=new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        System.Collections.Specialized.NotifyCollectionChangedEventHandler recordReorder=(_,e)=>reorderActions.Add(e.Action);
        selectionVm.Icons.CollectionChanged+=recordReorder;
        Check("Icon reorder matches Windows path casing and preserves canonical path",
            selectionVm.Reorder(sameNameB.Path.ToUpperInvariant(),sameNameA.Path.ToLowerInvariant())
            && selectionSettings.IconOrder[0]==sameNameB.Path);
        selectionVm.Icons.CollectionChanged-=recordReorder;
        Check("Icon reorder emits one move instead of replacing cells",reorderActions.SequenceEqual(new[]{System.Collections.Specialized.NotifyCollectionChangedAction.Move}));
        Check("Dropping on same identity with different casing is ignored",
            !selectionVm.Reorder(sameNameB.Path,sameNameB.Path.ToUpperInvariant(),true));
        Check("Drop preview refreshes ranks after reorder",
            !selectionVm.CanReorder(sameNameB.Path,sameNameA.Path)
            && selectionVm.CanReorder(sameNameA.Path,sameNameB.Path));
        selectionSettings.IconOrder[0]=sameNameA.Path;
        selectionSettings.IconOrder[1]=sameNameB.Path;
        Check("Drop preview detects in-place order changes",
            selectionVm.CanReorder(sameNameB.Path,sameNameA.Path)
            && !selectionVm.CanReorder(sameNameA.Path,sameNameB.Path));
        selectionSettings.IconOrder=[sameNameB.Path,sameNameA.Path];
        var replacementB=new DesktopIcon {Name="Browser",Path=@"c:\public\browser.lnk"};
        allIconsField.SetValue(selectionVm,new[]{replacementB,sameNameA});
        // Model the selection reset issued by WPF while an item is replaced.
        selectionVm.Icons.CollectionChanged+=(_,_)=>selectionVm.SelectedIcon=null;
        selectionVm.RefreshOrder();
        Check("Desktop refresh restores selection by path, not duplicate name or index",
            ReferenceEquals(selectionVm.SelectedIcon,replacementB));
        selectionVm.SearchQuery="missing";
        Check("Filtered icon count includes total and recovery copy",
            selectionVm.CountText=="0 / 2 mục" && selectionVm.HasSearch && selectionVm.EmptyTitle=="Không có kết quả");
        Check("Empty search copy refers only to search, not nonexistent filters",selectionVm.EmptyHint=="Thử tên ngắn hơn hoặc từ khóa khác.");
        Check("Filtering selected icon out clears selection",selectionVm.SelectedIcon==null);
        selectionVm.SearchQuery=""; selectionVm.SelectedIcon=replacementB;
        Check("Clearing icon search restores unfiltered count",selectionVm.CountText=="2 mục" && !selectionVm.HasSearch);
        allIconsField.SetValue(selectionVm,new[]{sameNameA}); selectionVm.RefreshOrder();
        Check("Deleted selected icon does not select unrelated item",selectionVm.SelectedIcon==null);
        Check("Drop preview discards deleted desktop identities",!selectionVm.CanReorder(sameNameB.Path,sameNameA.Path));
        var hiddenIcon=new DesktopIcon {Name="Document",Path=@"C:\User\Document.txt"};
        allIconsField.SetValue(selectionVm,new[]{sameNameA,hiddenIcon,sameNameB});
        selectionSettings.IconOrder=[sameNameA.Path,hiddenIcon.Path,sameNameB.Path];
        selectionVm.SearchQuery="Browser";
        Check("Filtered keyboard move relocates selected source, not its neighbor",
            selectionVm.MoveVisible(sameNameA.Path,1)
            && selectionSettings.IconOrder.SequenceEqual(new[]{hiddenIcon.Path,sameNameB.Path,sameNameA.Path}));
        Check("Filtered keyboard move respects visible boundaries",
            !selectionVm.MoveVisible(sameNameA.Path,1)
            && !selectionVm.MoveVisible(sameNameB.Path,-1)
            && !selectionVm.MoveVisible("foreign",1)
            && !selectionVm.MoveVisible(sameNameA.Path,0));
        Check("Filtered keyboard move up preserves unrelated hidden item",
            selectionVm.MoveVisible(sameNameA.Path,-1)
            && selectionSettings.IconOrder.SequenceEqual(new[]{hiddenIcon.Path,sameNameA.Path,sameNameB.Path}));
        Check("Desktop search matches Vietnamese without accents",
            SearchText.Matches("Đồ án tốt nghiệp.docx",SearchText.Words("do an")));
        Check("Desktop search requires all words regardless of order",
            SearchText.Matches("Tài liệu dự án",SearchText.Words("AN\t tai"))
            && !SearchText.Matches("Tài liệu",SearchText.Words("tai anh")));
        Check("Desktop search handles decomposed Vietnamese and empty query",
            SearchText.Matches("Ta\u0300i lie\u0323\u0302u",SearchText.Words("tài liệu"))
            && SearchText.Matches("Control Panel",SearchText.Words("  ")));
        Check("Search tolerates malformed Shell display names",
            SearchText.Matches("Tài\uD800 liệu.txt",SearchText.Words("tai lieu"))
            && SearchText.Matches("Đồ\uDC00 án",SearchText.Words("do an")));
        Check("Search preserves valid emoji pairs",
            SearchText.Matches("📁 Tài liệu",SearchText.Words("📁 tai")));
        Check("Grid drop splits tile at its center",
            IconGridPanel.CalculateInsertion(49,20,5,3,80,90,10)==0
            && IconGridPanel.CalculateInsertion(50,20,5,3,80,90,10)==1);
        Check("Grid drop accepts gutter between tiles",
            IconGridPanel.CalculateInsertion(95,20,5,3,80,90,10)==1);
        Check("Grid drop accepts empty cells in final row",
            IconGridPanel.CalculateInsertion(240,110,5,3,80,90,10)==5);
        Check("Grid drop accepts empty area below final row",
            IconGridPanel.CalculateInsertion(15,300,5,3,80,90,10)==5);
        Check("Grid drop accounts for scrolled content coordinates",
            IconGridPanel.CalculateInsertion(15,200,20,3,80,90,10)==6);
        Check("Grid drop rejects invalid geometry and empty lists",
            IconGridPanel.CalculateInsertion(double.NaN,20,5,3,80,90,10)==-1
            && IconGridPanel.CalculateInsertion(15,20,0,3,80,90,10)==-1);
        var gestureStart=new Point(100,100);
        Check("Icon click tolerates movement below Windows drag threshold",
            !IconPanel.ExceedsDragThreshold(gestureStart,new Point(100+SystemParameters.MinimumHorizontalDragDistance/2,100+SystemParameters.MinimumVerticalDragDistance/2)));
        Check("Icon horizontal drag follows Windows threshold",
            IconPanel.ExceedsDragThreshold(gestureStart,new Point(100+SystemParameters.MinimumHorizontalDragDistance,100)));
        Check("Icon vertical drag follows Windows threshold in either direction",
            IconPanel.ExceedsDragThreshold(gestureStart,new Point(100,100-SystemParameters.MinimumVerticalDragDistance)));
        var iconVm = (DesktopIconViewModel)icons.DataContext;
        using var layoutIconsService=new DesktopIconService(_=>Task.FromResult(new List<DesktopIcon>()));
        iconVm.Initialize(layoutIconsService,preview.Settings);
        Pump(30);
        foreach (var name in new[] { "Tài liệu công việc", "Dự án cá nhân", "Trình duyệt", "Ghi chú hôm nay", "Ảnh & cảm hứng", "Recycle Bin" })
        {
            var drawing = new GeometryDrawing(new SolidColorBrush(Color.FromRgb(217,188,139)), null, new RectangleGeometry(new Rect(3,6,27,22),4,4));
            var image = new DrawingImage(drawing); image.Freeze();
            iconVm.Icons.Add(new DesktopIcon { Name = name, Path = "demo/" + name + ".lnk", Icon = image });
        }
        typeof(DesktopIconViewModel).GetField("_all",BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(iconVm,iconVm.Icons.ToArray());
        typeof(DesktopIconViewModel).GetField("_loading", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(iconVm, false);
        icons.DataContext = null; icons.DataContext = iconVm;
        ((System.Windows.Controls.TextBlock)preview.FindName("FooterState")).Text = "Bản xem trước · số liệu minh họa";
        Render((FrameworkElement)preview.Content, 380, 880, Path.Combine(output, "nepply-graphite.png"));
        var scroller = (System.Windows.Controls.ScrollViewer)preview.FindName("MonitorScroller");
        var summaryGrid = (System.Windows.Controls.Primitives.UniformGrid)monitorPanel.FindName("SummaryGrid");
        Check("Monitor details removed",monitorPanel.FindName("DetailsExpander")==null && monitorPanel.FindName("DetailsNavigation")==null);
        preview.Settings.SummaryColumns = 1; preview.Settings.GpuColumns = 2; preview.ApplySettings();
        Check("Card shape settings keep all metric cards equal", summaryGrid.Columns == 1 && monitorPanel.GpuColumns == 1);
        preview.Settings.SummaryColumns = 0; preview.Settings.GpuColumns = 0; preview.ApplySettings();
        Render((FrameworkElement)preview.Content,380,880,Path.Combine(output,"layout-auto.png"));
        Check("Automatic cards reserve two equal GPU cells", summaryGrid.Columns == 2 && monitorPanel.GpuColumns == 2);
        DropHint.Show(summaryGrid,false,false);
        Check("Drop marker added without changing layout", System.Windows.Documents.AdornerLayer.GetAdornerLayer(summaryGrid)?.GetAdorners(summaryGrid)?.OfType<DropHint>().Count() == 1);
        Render((FrameworkElement)preview.Content,380,880,Path.Combine(output,"drop-target.png"));
        DropHint.Clear();
        Check("Drop marker cleaned after drag", System.Windows.Documents.AdornerLayer.GetAdornerLayer(summaryGrid)?.GetAdorners(summaryGrid)?.OfType<DropHint>().Any() != true);
        var dropLayer=System.Windows.Documents.AdornerLayer.GetAdornerLayer(summaryGrid)!;
        DropHint.Show(summaryGrid,false,false);
        summaryGrid.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Check("Drop marker removed on target unload",dropLayer.GetAdorners(summaryGrid)?.OfType<DropHint>().Any()!=true);
        DropHint.Show(summaryGrid,true,false);
        DropHint.Clear(); DropHint.Clear();
        Check("Drop marker cleanup can repeat",dropLayer.GetAdorners(summaryGrid)?.OfType<DropHint>().Any()!=true);
        preview.Settings.WidgetOrder = ["network", "summary", "gpus", "details"]; preview.ApplySettings();
        Check("Monitor custom order applied", monitorPanel.GetWidgetOrder().First() == "network");
        preview.Settings.ShowNetwork = false; preview.ApplySettings();
        Check("Network widget can be hidden", ((System.Windows.Controls.StackPanel)monitorPanel.FindName("Widgets")).Children.OfType<FrameworkElement>().Single(e => Equals(e.Tag,"network")).Visibility == Visibility.Collapsed);
        preview.Settings.ShowNetwork = true;
        preview.Settings.IconsFirst = true; preview.ApplySettings();
        Check("Icon section can precede monitor", System.Windows.Controls.Grid.GetRow(icons) == 0);
        preview.Settings.IconsFirst = false;
        preview.Settings.WidgetOrder = ["summary", "gpus", "network", "details"]; preview.ApplySettings();
        var gridPanel = FindVisual<IconGridPanel>(icons)!;
        var editButton=(System.Windows.Controls.Button)preview.FindName("EditLayoutButton");
        typeof(DesktopIconViewModel).GetField("_all",BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(iconVm,iconVm.Icons.ToArray());
        var beforeLayout=preview.Settings.Clone();
        var beforeIconOrder=iconVm.Icons.Select(i=>i.Path).ToArray();
        editButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check("Layout editing has no grid controls",icons.FindName("GridEditor")==null);
        Check("Icon reorder participates in layout draft",iconVm.Reorder(beforeIconOrder[0],beforeIconOrder[2],true) && iconVm.Icons[2].Path==beforeIconOrder[0]);
        ((System.Windows.Controls.Button)preview.FindName("SwapSectionsButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check("Panel editor swaps section order in its draft",preview.Settings.IconsFirst!=beforeLayout.IconsFirst);
        Check("Keyboard widget change participates in edit session",WidgetDrag.MoveByKeyboard(summaryGrid,1) && !preview.Settings.WidgetOrder.SequenceEqual(beforeLayout.WidgetOrder));
        preview.FinishLayout(false);
        Check("Cancel restores keyboard-reordered widgets",preview.Settings.WidgetOrder.SequenceEqual(beforeLayout.WidgetOrder));
        Check("Cancel layout restores dimensions and section order",preview.Settings.GridCellWidth==beforeLayout.GridCellWidth && preview.Settings.IconsFirst==beforeLayout.IconsFirst);
        Check("Cancel restores icon order in settings and visible collection",preview.Settings.IconOrder.SequenceEqual(beforeLayout.IconOrder) && iconVm.Icons.Select(i=>i.Path).SequenceEqual(beforeIconOrder));
        editButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        iconVm.Reorder(beforeIconOrder[0],beforeIconOrder[2],true);
        preview.FinishLayout(true);
        Check("Done layout retains draft and closes toolbar",preview.Settings.GridColumns==beforeLayout.GridColumns && ((FrameworkElement)preview.FindName("LayoutActions")).Visibility==Visibility.Collapsed);
        Check("Done retains icon order and vertical grid dimension",preview.Settings.GridRows==beforeLayout.GridRows && preview.Settings.IconOrder.Length>2 && preview.Settings.IconOrder[2]==beforeIconOrder[0] && iconVm.Icons[2].Path==beforeIconOrder[0]);
        preview.Settings.CopyFrom(beforeLayout); preview.ApplySettings(); icons.RefreshLayout(); icons.UpdateLayout();
        Check("Grid renders icons", gridPanel != null && VisualTreeHelper.GetChildrenCount(gridPanel) > 0);
        preview.Settings.GridCellWidth=72; preview.ApplySettings();
        Render(icons,310,180,Path.Combine(output,"grid-short.png"));
        double shortGridWidth=gridPanel!.ViewportWidth;
        int shortColumns=gridPanel.ColumnCount;
        Render(icons,310,700,Path.Combine(output,"grid-tall.png"));
        Check("Changing desktop section height preserves grid columns",shortGridWidth==gridPanel.ViewportWidth && shortColumns==gridPanel.ColumnCount);
        preview.Settings.CopyFrom(beforeLayout); preview.ApplySettings();
        Render((FrameworkElement)preview.Content,380,880,Path.Combine(output,"grid-restored.png"));
        Check("Icon cells fit effective columns", ((FrameworkElement)VisualTreeHelper.GetChild(gridPanel!,0)).ActualWidth <= gridPanel.ViewportWidth/gridPanel.ColumnCount+.01);
        gridPanel!.SetVerticalOffset(0); icons.UpdateLayout();
        var firstGridChild=(Visual)VisualTreeHelper.GetChild(gridPanel,0);
        var childRequest=new Rect(0,0,8,8);
        var expectedVisible=firstGridChild.TransformToAncestor(gridPanel).TransformBounds(childRequest);
        expectedVisible.Intersect(new Rect(0,0,gridPanel.ViewportWidth,gridPanel.ViewportHeight));
        Check("Grid MakeVisible returns panel coordinates",gridPanel.MakeVisible(firstGridChild,childRequest)==expectedVisible);
        Check("Grid MakeVisible rejects detached visuals",gridPanel.MakeVisible(new System.Windows.Controls.Border(),childRequest).IsEmpty);
        Check("Grid MakeVisible rejects empty requests",gridPanel.MakeVisible(firstGridChild,Rect.Empty).IsEmpty);
        icons.SetEditMode(true);
        icons.UpdateLayout();
        Check("Layout editing preserves grid intent with adaptive density", gridPanel!.Columns==preview.Settings.GridColumns && gridPanel.Rows==preview.Settings.GridRows && gridPanel.ColumnCount<=gridPanel.Columns);
        ((System.Windows.Controls.ListBox)icons.FindName("IconList")).SelectedIndex = 0;
        Render((FrameworkElement)preview.Content,380,880,Path.Combine(output,"grid-editor.png"));
        icons.SetEditMode(false);
        preview.Settings.GridCellWidth = 88; preview.Settings.GridCellHeight = 90; preview.ApplySettings();
        for (int i = 0; i < 1000; i++) iconVm.Icons.Add(new DesktopIcon { Name = "Test " + i, Path = "demo/" + i });
        icons.UpdateLayout();
        Check("Thousand icons bounded containers", VisualTreeHelper.GetChildrenCount(gridPanel!) < 60);
        gridPanel!.SetVerticalOffset(double.PositiveInfinity); icons.UpdateLayout();
        Check("Grid reaches last row", Math.Abs(gridPanel.VerticalOffset - (gridPanel.ExtentHeight - gridPanel.ViewportHeight)) < 1);
        Check("Scrolled grid remains bounded", VisualTreeHelper.GetChildrenCount(gridPanel) < 60);
        iconVm.Icons.RemoveAt(1005); icons.UpdateLayout();
        Check("Grid removal while scrolled safe", true);
        iconVm.Icons[1004] = new DesktopIcon { Name = "Replaced", Path = "demo/replaced" }; icons.UpdateLayout();
        iconVm.Icons.Move(1004, 0); icons.UpdateLayout();
        Check("Grid replacement and reorder safe", true);
        var originalIcons = iconVm.Icons.Take(6).ToArray(); iconVm.Icons.Clear(); icons.UpdateLayout();
        Check("Empty grid releases containers", VisualTreeHelper.GetChildrenCount(gridPanel) == 0);
        foreach (var icon in originalIcons) iconVm.Icons.Add(icon); icons.UpdateLayout();
        // Earlier standalone renders set local dimensions. Release them before testing the real parent layout.
        icons.ClearValue(FrameworkElement.WidthProperty);
        icons.ClearValue(FrameworkElement.HeightProperty);
        foreach (var edge in new[] { EdgePosition.Left, EdgePosition.Right })
        foreach (int headerWidth in new[] { 320, 520 })
        {
            preview.Settings.EdgePosition=edge; preview.ApplySettings();
            Render((FrameworkElement)preview.Content,headerWidth,880,Path.Combine(output,$"header-{edge}-{headerWidth}-closed.png"));
            var header=(FrameworkElement)preview.FindName("PanelHeader");
            var content=(FrameworkElement)preview.FindName("ContentGrid");
            var beforeOrigin=content.TranslatePoint(new Point(),(UIElement)preview.Content);
            double beforeHeight=content.ActualHeight;
            desktopSearchVm.SearchQuery="tài liệu"; Pump(30);
            Render((FrameworkElement)preview.Content,headerWidth,880,Path.Combine(output,$"header-{edge}-{headerWidth}-search.png"));
            var root=(Visual)preview.Content;
            var editorBounds=desktopSearchEditor.TransformToAncestor(root).TransformBounds(new Rect(desktopSearchEditor.RenderSize));
            var settingsButton=(FrameworkElement)preview.FindName("PanelSettingsButton");
            var settingsBounds=settingsButton.TransformToAncestor(root).TransformBounds(new Rect(settingsButton.RenderSize));
            Check($"{edge}/{headerWidth} search preserves header and content geometry",header.ActualHeight==40
                && beforeOrigin==content.TranslatePoint(new Point(),(UIElement)preview.Content) && Math.Abs(beforeHeight-content.ActualHeight)<.5);
            Check($"{edge}/{headerWidth} search fits without covering settings",editorBounds.Left>=0 && editorBounds.Right<=settingsBounds.Left
                && settingsBounds.Right<=headerWidth && desktopSearchEditor.ActualWidth>=180 && settingsButton.Visibility==Visibility.Visible);
            ((FrameworkElement)preview.FindName("MainPanel")).RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.Input.Mouse.PreviewMouseDownEvent});
            Pump(250);
            Check($"{edge}/{headerWidth} blank click restores toolbar and clears query",desktopSearchVm.SearchQuery==""
                && desktopSearchEditor.Visibility==Visibility.Collapsed && editButton.Visibility==Visibility.Visible);
        }
        var sideSplitter=(System.Windows.Controls.GridSplitter)preview.FindName("ContentSeparator");
        var sideGrip=(FrameworkElement)sideSplitter.Template.FindName("Grip",sideSplitter);
        Check("Side section divider grip stays horizontal",sideGrip.ActualWidth>sideGrip.ActualHeight);
        preview.Settings.EdgePosition = EdgePosition.Top; preview.ApplySettings();
        preview.PositionWindow();
        Check("Top layout is wide and shallow", preview.Width > preview.Height * 2 && preview.Height == 320);
        Render((FrameworkElement)preview.Content, 1440, 320, Path.Combine(output, "nepply-top.png"));
        foreach(int topWidth in new[] { 520, 960, 1440 })
        foreach(int topHeight in new[] { 240, 320 })
        foreach(bool iconsFirst in new[] { false, true })
        {
            preview.Settings.IconsFirst=iconsFirst; preview.ApplySettings();
            Render((FrameworkElement)preview.Content,topWidth,topHeight,Path.Combine(output,$"top-layout-{topWidth}-{topHeight}-{iconsFirst}.png"));
            var host=(System.Windows.Controls.Grid)preview.FindName("ContentGrid");
            var bounds=icons.TransformToAncestor(host).TransformBounds(new Rect(icons.RenderSize));
            int column=iconsFirst ? 0 : 2;
            Check($"Top {topWidth}/{topHeight}/{iconsFirst} monitor respects actual width after reconfiguration",
                host.ColumnDefinitions[iconsFirst ? 2 : 0].ActualWidth<=host.ActualWidth*.4+.5);
            Check($"Top {topWidth}/{topHeight}/{iconsFirst} desktop fills its actual column",double.IsNaN(icons.Width) && double.IsNaN(icons.Height)
                && Math.Abs(icons.ActualWidth-host.ColumnDefinitions[column].ActualWidth)<1 && bounds.Left>=-.5 && bounds.Right<=host.ActualWidth+.5);
            Check($"Top {topWidth}/{topHeight}/{iconsFirst} retains usable icon viewport",gridPanel.ViewportHeight>=80 && gridPanel.ColumnCount>=1
                && VisualTreeHelper.GetChildrenCount(gridPanel)>0);
            var topScroller=(System.Windows.Controls.ScrollViewer)preview.FindName("MonitorScroller");
            // 720 DIP is the narrowest horizontal dock Settings allows (TopPanelWidth clamp);
            // the 520 stress size keeps one card column and may scroll.
            if(topHeight==320 && topWidth>=720) Check($"Top {topWidth}/{topHeight}/{iconsFirst} monitor cards fit without scrolling ({topScroller.ExtentHeight:F0}/{topScroller.ViewportHeight:F0})",
                topScroller.ViewportHeight>0 && topScroller.ExtentHeight<=topScroller.ViewportHeight+.5);
            var splitter=(System.Windows.Controls.GridSplitter)preview.FindName("ContentSeparator");
            var grip=(FrameworkElement)splitter.Template.FindName("Grip",splitter);
            Check($"Top {topWidth}/{topHeight}/{iconsFirst} section divider grip is vertical",grip.ActualHeight>grip.ActualWidth);
        }
        preview.Settings.IconsFirst=false; preview.ApplySettings();
        Render((FrameworkElement)preview.Content,1440,320,Path.Combine(output,"nepply-top-actual-grid.png"));
        Check("Top monitor uses compact presentation", Nepply.Modules.Appearance.PresentationDensity.GetCompact(monitorPanel));
        var compactCard=(System.Windows.Controls.Border)summaryGrid.Children[0];
        double compactHeight=summaryGrid.ActualHeight;
        Check("Top compact density inherits into card templates",compactCard.Padding==new Thickness(8,6,8,6));
        int retainedGpuCount=vm.Gpus.Count;
        monitorPanel.ConfigureLayout(preview.Settings,true);
        Render((FrameworkElement)preview.Content,1440,320,Path.Combine(output,"nepply-top-edit-density.png"));
        Check("Editing restores full metric cards without replacing data",!Nepply.Modules.Appearance.PresentationDensity.GetCompact(monitorPanel)
            && compactCard.Padding==new Thickness(11,9,11,9) && summaryGrid.ActualHeight>compactHeight && vm.Gpus.Count==retainedGpuCount);
        monitorPanel.ConfigureLayout(preview.Settings,false);
        Render((FrameworkElement)preview.Content,1440,320,Path.Combine(output,"nepply-top-compact.png"));
        Check("Leaving edit restores compact cards",summaryGrid.ActualHeight==compactHeight && vm.Gpus.Count==retainedGpuCount);
        Check("Top panel has no details overlay",preview.FindName("DetailLayer")==null && monitorPanel.FindName("DetailsNavigation")==null);
        var islandGrid=(System.Windows.Controls.Grid)preview.FindName("ContentGrid");
        double islandContentHeight=islandGrid.ActualHeight;
        preview.FocusDesktopSearch();
        Render((FrameworkElement)preview.Content, 1440, 320, Path.Combine(output, "nepply-top-search.png"));
        var islandSearch=(System.Windows.Controls.Border)preview.FindName("DesktopSearchEditor");
        Check("Island search stays in toolbar row",System.Windows.Controls.Grid.GetRow(islandSearch)==0);
        Check("Island search does not shrink content",Math.Abs(islandContentHeight-islandGrid.ActualHeight)<.5);
        static int VisibleSearchGlyphs(DependencyObject root)
        {
            int count=0;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            {
                var child=VisualTreeHelper.GetChild(root,i);
                if(child is UIElement element && element.Visibility!=Visibility.Visible) continue; // Preview window is not shown.
                if(child is System.Windows.Controls.TextBlock {Text:""}) count++;
                count+=VisibleSearchGlyphs(child);
            }
            return count;
        }
        Check("Open search shows one magnifier: the highlighted header button",
            VisibleSearchGlyphs((DependencyObject)preview.FindName("PanelHeader"))==1
            && ((FrameworkElement)preview.FindName("DesktopSearchButton")).Visibility==Visibility.Visible);
        Check("Island search replaces brand without overlapping icon",((System.Windows.Controls.Image)preview.FindName("BrandTitle")).Visibility==Visibility.Hidden);
        Check("Panel brand loads embedded transparent icon",((System.Windows.Controls.Image)preview.FindName("BrandTitle")).Source is BitmapSource {PixelWidth:>0});
        Render((FrameworkElement)preview.Content, 520, 320, Path.Combine(output, "nepply-top-search-narrow.png"));
        var searchBounds=islandSearch.TransformToAncestor((Visual)preview.Content).TransformBounds(new Rect(islandSearch.RenderSize));
        Check("Island search fits narrow high DPI surface",searchBounds.Left>=0 && searchBounds.Right<=520 && islandSearch.ActualWidth>=100);
        Check("Island monitor adapts to actual narrow surface",islandGrid.ColumnDefinitions[0].ActualWidth<=520*.4+.5);
        preview.Settings.ShowMonitor = false; preview.ApplySettings();
        var grid = (System.Windows.Controls.Grid)preview.FindName("ContentGrid");
        Check("Top layout removes monitor column when disabled", grid.ColumnDefinitions[0].Width.Value == 0);
        StartupSettingsTests.Run(Check);
        var settingsWindow = new SettingsWindow(new AppSettings(), adapters);
        var delayControl=(System.Windows.Controls.Slider)settingsWindow.FindName("DelaySlider");
        Check("Delay slider has scale-appropriate keyboard steps",delayControl.SmallChange==50 && delayControl.LargeChange==250);
        Check("Settings sliders retain continuous pointer adjustment",!delayControl.IsSnapToTickEnabled);
        DotGridPickerTests.Run(Check,Pump,output,Render);
        var customHex=(System.Windows.Controls.TextBox)settingsWindow.FindName("CustomAccent");
        var accentChoice=(System.Windows.Controls.ComboBox)settingsWindow.FindName("AccentChoice");
        var readSettingsDraft=typeof(SettingsWindow).GetMethod("ReadDraft",BindingFlags.Instance|BindingFlags.NonPublic)!;
        customHex.Text="#12"; accentChoice.SelectedItem="Neon Cyan";
        Check("Named palette ignores incomplete inactive HEX",Equals(readSettingsDraft.Invoke(settingsWindow,null),true));
        var colorDraft=(AppSettings)typeof(SettingsWindow).GetProperty("_draft",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(settingsWindow)!;
        Check("Inactive HEX preserves valid custom draft",colorDraft.CustomAccent=="#D9BC8B");
        accentChoice.SelectedItem="Custom";
        Check("Custom palette rejects incomplete HEX",Equals(readSettingsDraft.Invoke(settingsWindow,null),false));
        customHex.Text="#123456";
        Check("Custom palette accepts corrected HEX",Equals(readSettingsDraft.Invoke(settingsWindow,null),true) && colorDraft.CustomAccent=="#123456");
        customHex.Text="#D9BC8B"; accentChoice.SelectedItem="Champagne";
        ((System.Windows.Controls.TextBlock)settingsWindow.FindName("ErrorText")).Text="";
        var appearanceSection=(FrameworkElement)settingsWindow.FindName("AppearanceSection");
        appearanceSection.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(.6,1,TimeSpan.FromSeconds(2)));
        settingsWindow.ShowSettingsSection(1);
        Check("Settings navigation removes animation from hidden section",!appearanceSection.HasAnimatedProperties && appearanceSection.Visibility==Visibility.Collapsed);
        settingsWindow.ShowSettingsSection(0);
        Check("Hidden settings window does not start section animation",!appearanceSection.HasAnimatedProperties);
        var searchBox=(System.Windows.Controls.TextBox)settingsWindow.FindName("SearchSettings");
        Pump(30);
        Check("Settings search starts collapsed to an icon",searchBox.Visibility==Visibility.Collapsed);
        var searchResults=(System.Windows.Controls.ListBox)settingsWindow.FindName("SearchResults");
        var searchDraft=(System.Windows.Controls.Slider)settingsWindow.FindName("OpacitySlider");
        searchDraft.Value=91;
        searchBox.Text="luoi doc";
        Pump(30);
        Check("Settings search stays expanded for a query",searchBox.Visibility==Visibility.Visible);
        Check("Settings search reveals matching results",searchResults.Visibility==Visibility.Visible && searchResults.Items.Count==1);
        searchResults.SelectedIndex=0; Pump(50);
        Check("Selecting search result does not navigate before activation",searchBox.Text=="luoi doc" && searchResults.Visibility==Visibility.Visible);
        settingsWindow.ActivateSearchSelection(); Pump(50);
        Check("Search result navigates to Desktop settings",((FrameworkElement)settingsWindow.FindName("DesktopSection")).Visibility==Visibility.Visible);
        Check("Settings search preserves unsaved draft",searchDraft.Value==91);
        Check("Search closes results after navigation",searchBox.Text=="" && searchResults.Visibility==Visibility.Collapsed);
        searchBox.Text="no_such_setting";
        Check("Settings search exposes no-result message",((System.Windows.Controls.TextBlock)settingsWindow.FindName("SearchHint")).Text.Contains(LocalizationService.Get("NoResults")));
        searchBox.Text="gpu";
        Render((FrameworkElement)settingsWindow.Content,520,500,Path.Combine(output,"settings-search-small.png"));
        searchBox.Clear(); settingsWindow.ShowSettingsSection(0);
        var diagnosticReport=Nepply.Services.Telemetry.SensorDiagnosticReport.Build(HardwareInfoSnapshot.Empty with
        {Sequence=1,MonotonicTimestamp=Stopwatch.GetTimestamp(),Hardware=HardwareReading.Empty with
        {CpuName="CPU minh họa"}},"PrivilegeUser","DriverAbsent",Stopwatch.GetTimestamp());
        var diagnosticWindow=new SensorDiagnosticsWindow(diagnosticReport);
        Render((FrameworkElement)diagnosticWindow.Content,460,440,Path.Combine(output,"diagnostics-small.png"));
        var diagnosticText=(System.Windows.Controls.TextBox)diagnosticWindow.FindName("ReportText");
        Check("Diagnostics report is selectable read-only text",diagnosticText.IsReadOnly && diagnosticText.Text==diagnosticReport);
        Check("Diagnostics supports compact viewport",diagnosticText.ActualHeight>100 && diagnosticText.ActualWidth>300);
        diagnosticWindow.Close();
        var removedOptions=new SettingsWindow(new AppSettings(),adapters);
        Check("Detail-only options removed from settings",removedOptions.FindName("ShowDetails")==null && removedOptions.FindName("ShowStorage")==null && removedOptions.FindName("BoardSensorChoice")==null);
        removedOptions.Close();
        var profileTarget=new AppSettings();
        var profileWindow=new SettingsWindow(profileTarget,adapters);
        ((System.Windows.Controls.CheckBox)profileWindow.FindName("AutoStart")).IsChecked=true;
        bool importedProfile=profileWindow.ImportLayout(new LayoutProfile {CellWidth=119,Edge=EdgePosition.Top}.ToJson());
        Check("Profile import keeps panel layout in draft",importedProfile && ((AppSettings)typeof(SettingsWindow).GetProperty("_draft",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(profileWindow)!).GridCellWidth==119);
        Check("Profile import preserves unrelated unsaved input",((System.Windows.Controls.CheckBox)profileWindow.FindName("AutoStart")).IsChecked==true);
        Check("Profile import leaves live settings untouched",profileTarget.GridCellWidth==88 && !profileTarget.AutoStart && profileTarget.EdgePosition==EdgePosition.Left);
        ((System.Windows.Controls.ComboBox)profileWindow.FindName("AccentChoice")).SelectedItem="Neon Cyan";
        bool appliedPreset=profileWindow.ApplyLayoutPreset("ribbon");
        double presetHeightPercent=280/Nepply.Modules.Panel.PanelDimensions.Available(Nepply.Helpers.ScreenManager.Select(profileTarget.MonitorDevice)).Height*100;
        Check("Preset updates draft controls",appliedPreset
            && ((System.Windows.Controls.ComboBox)profileWindow.FindName("EdgeChoice")).SelectedIndex==(int)EdgePosition.Top
            && Math.Abs(((System.Windows.Controls.Slider)profileWindow.FindName("HeightSlider")).Value-presetHeightPercent)<.001);
        Check("Preset preserves unrelated unsaved controls",((System.Windows.Controls.CheckBox)profileWindow.FindName("AutoStart")).IsChecked==true
            && (string?)((System.Windows.Controls.ComboBox)profileWindow.FindName("AccentChoice")).SelectedItem=="Neon Cyan");
        Check("Preset selection leaves live settings untouched",profileTarget.EdgePosition==EdgePosition.Left && !profileTarget.AutoStart);
        Check("Unknown preset leaves draft untouched",!profileWindow.ApplyLayoutPreset("missing")
            && Math.Abs(((System.Windows.Controls.Slider)profileWindow.FindName("HeightSlider")).Value-presetHeightPercent)<.001);
        Check("Preset cards expose three starting layouts",((System.Windows.Controls.ItemsControl)profileWindow.FindName("PresetCards")).Items.Count==3);
        string aboutVersion=typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Check("Settings identifies version and publisher without choosing a source license",
            ((System.Windows.Controls.TextBlock)profileWindow.FindName("AboutVersion")).Text=="Nepply "+aboutVersion+" · Minh Quang");
        Check("Documentation resolver rejects arbitrary files",SettingsWindow.FindLocalDocument("settings.json",AppContext.BaseDirectory)==null);
        Check("Packaged privacy document is discoverable",SettingsWindow.FindLocalDocument("PRIVACY.md",AppContext.BaseDirectory)!=null);
        profileWindow.ShowSettingsSection(4);
        Render((FrameworkElement)profileWindow.Content,520,500,Path.Combine(output,"settings-profiles.png"));
        profileWindow.Close();
        Check("Closing imported draft does not apply profile",profileTarget.GridCellWidth==88 && !profileTarget.AutoStart);
        var welcomeWindow = new WelcomeWindow(new AppSettings());
        Render((FrameworkElement)welcomeWindow.Content,560,650,Path.Combine(output,"welcome.png"));
        Check("Welcome does not preselect Desktop hiding",((System.Windows.Controls.CheckBox)welcomeWindow.FindName("HideIcons")).IsChecked==false);
        Render((FrameworkElement)welcomeWindow.Content,460,480,Path.Combine(output,"welcome-small.png"));
        Check("Welcome supports compact window",welcomeWindow.MinWidth==460 && welcomeWindow.MinHeight==480);
        var startButton=(FrameworkElement)welcomeWindow.FindName("StartButton");
        var startBounds=startButton.TransformToAncestor((System.Windows.Media.Visual)welcomeWindow.Content).TransformBounds(new Rect(startButton.RenderSize));
        Check("Welcome action remains inside compact viewport",startBounds.Right<=460 && startBounds.Bottom<=480 && startBounds.Top>=0 && startButton.ActualHeight>=32);
        welcomeWindow.Close();
        Render((FrameworkElement)settingsWindow.Content, 620, 700, Path.Combine(output, "nepply-settings.png"));
        var navigation=(System.Windows.Controls.ListBox)settingsWindow.FindName("SettingsNavigation");
        Render((FrameworkElement)settingsWindow.Content,820,740,Path.Combine(output,"settings-wide-rail.png"));
        Check("Wide settings use a navigation rail",System.Windows.Controls.Grid.GetColumn(navigation)==0 && System.Windows.Controls.Grid.GetRowSpan(navigation)==2);
        (FrameworkElement Bar,FrameworkElement Underline) SelectedIndicators()
        {
            var item=(System.Windows.Controls.ListBoxItem)navigation.ItemContainerGenerator.ContainerFromItem(navigation.SelectedItem);
            return ((FrameworkElement)item.Template.FindName("SelectionIndicator",item),(FrameworkElement)item.Template.FindName("SelectionUnderline",item));
        }
        var railIndicators=SelectedIndicators();
        Check("Settings rail marks the selected section with a vertical bar",railIndicators.Bar.Visibility==Visibility.Visible
            && railIndicators.Underline.Visibility==Visibility.Collapsed && railIndicators.Bar.ActualHeight>railIndicators.Bar.ActualWidth);
        Render((FrameworkElement)settingsWindow.Content,620,700,Path.Combine(output,"settings-adaptive-tabs.png"));
        Check("Narrow settings return to compact tabs",System.Windows.Controls.Grid.GetColumn(navigation)==1 && System.Windows.Controls.Grid.GetRowSpan(navigation)==1);
        var tabIndicators=SelectedIndicators();
        Check("Settings tabs mark the selected section with an underline, not a vertical bar",tabIndicators.Bar.Visibility==Visibility.Collapsed
            && tabIndicators.Underline.Visibility==Visibility.Visible && tabIndicators.Underline.ActualWidth>tabIndicators.Underline.ActualHeight);
        var searchFocusWindow=new SettingsWindow(new AppSettings());
        var focusSearchBox=(System.Windows.Controls.TextBox)searchFocusWindow.FindName("SearchSettings");
        var focusResults=(System.Windows.Controls.ListBox)searchFocusWindow.FindName("SearchResults");
        var pendingSearchField=typeof(SettingsWindow).GetField("_searchFocusOperation",BindingFlags.Instance|BindingFlags.NonPublic)!;
        System.Windows.Threading.DispatcherOperation QueueSearchFocus()
        {
            focusSearchBox.Text="luoi doc";
            focusResults.SelectedIndex=0;
            searchFocusWindow.ActivateSearchSelection();
            return (System.Windows.Threading.DispatcherOperation)pendingSearchField.GetValue(searchFocusWindow)!;
        }
        var sectionSearchFocus=QueueSearchFocus();
        searchFocusWindow.ShowSettingsSection(0);
        Check("Changing settings section cancels queued search focus",sectionSearchFocus.Status==System.Windows.Threading.DispatcherOperationStatus.Aborted);
        var typingSearchFocus=QueueSearchFocus();
        focusSearchBox.Text="mau rieng";
        Check("Typing a new search cancels previous focus",typingSearchFocus.Status==System.Windows.Threading.DispatcherOperationStatus.Aborted);
        var replacedSearchFocus=QueueSearchFocus();
        var latestSearchFocus=QueueSearchFocus();
        Check("Only latest settings search keeps pending focus",replacedSearchFocus.Status==System.Windows.Threading.DispatcherOperationStatus.Aborted
            && latestSearchFocus.Status==System.Windows.Threading.DispatcherOperationStatus.Pending);
        searchFocusWindow.Close();
        Check("Closing settings cancels queued search focus",latestSearchFocus.Status==System.Windows.Threading.DispatcherOperationStatus.Aborted
            && pendingSearchField.GetValue(searchFocusWindow)==null);
        foreach(var searchQuery in new[] {"giao dien","panel","desktop"})
        {
            foreach(var entry in SettingsSearch.Find(searchQuery).Where(e=>e.Section<3))
                Check("Settings search target exists: "+entry.Control,settingsWindow.FindName(entry.Control) is FrameworkElement);
        }
        var widthControl=(System.Windows.Controls.Slider)settingsWindow.FindName("OpacitySlider");
        widthControl.Value=93;
        navigation.SelectedIndex=2;
        Render((FrameworkElement)settingsWindow.Content,520,500,Path.Combine(output,"settings-desktop-small.png"));
        Check("Settings navigation exposes only requested section",((FrameworkElement)settingsWindow.FindName("DesktopSection")).Visibility==Visibility.Visible && ((FrameworkElement)settingsWindow.FindName("AppearanceSection")).Visibility==Visibility.Collapsed);
        navigation.SelectedIndex=3;
        Render((FrameworkElement)settingsWindow.Content,620,700,Path.Combine(output,"settings-monitor.png"));
        navigation.SelectedIndex=2;
        Check("Settings navigation preserves unsaved draft",widthControl.Value==93);
        Check("Grid editing has one location",settingsWindow.FindName("CellWidthSlider")==null && settingsWindow.FindName("IconsFirst")==null && settingsWindow.FindName("GridPicker") is DotGridPicker);
        settingsWindow.ShowSettingsSection(4);
        Check("Programmatic settings navigation stays synchronized",navigation.SelectedIndex==4 && ((FrameworkElement)settingsWindow.FindName("ApplicationSection")).Visibility==Visibility.Visible);
        var hexField=(System.Windows.Controls.TextBox)settingsWindow.FindName("CustomAccent");
        accentChoice.SelectedItem="Custom";
        hexField.Text="invalid";
        typeof(SettingsWindow).GetMethod("Save_Click",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(settingsWindow,[settingsWindow,new RoutedEventArgs()]);
        Check("Invalid color navigates to its section without saving",navigation.SelectedIndex==0 && ((System.Windows.Controls.TextBlock)settingsWindow.FindName("ErrorText")).Text.Contains("#RRGGBB"));
        hexField.Text="#C2BE8D";
        navigation.SelectedIndex=0;
        Check("Settings uses custom window chrome", settingsWindow.WindowStyle == WindowStyle.None);
        var settingsQuery=(System.Windows.Controls.TextBox)settingsWindow.FindName("SearchSettings");
        foreach(var searchSize in new[] {new Size(520,500),new Size(820,740)})
        {
            settingsQuery.Clear();
            settingsWindow.ApplyCompactLayout(searchSize.Height);
            var root=(FrameworkElement)settingsWindow.Content;
            Render(root,(int)searchSize.Width,(int)searchSize.Height,Path.Combine(output,$"search-layout-{searchSize.Width}-before.png"));
            var form=(FrameworkElement)settingsWindow.FindName("SettingsScroll");
            var overlay=(FrameworkElement)settingsWindow.FindName("SearchOverlayLayer");
            Rect Bounds(FrameworkElement element)=>element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
            var formBefore=Bounds(form); var navigationBefore=Bounds(navigation);
            settingsQuery.Text="màu";
            Render(root,(int)searchSize.Width,(int)searchSize.Height,Path.Combine(output,$"search-layout-{searchSize.Width}-results.png"));
            checkSearchLayout();
            void checkSearchLayout()
            {
                Check($"Settings search {searchSize.Width} preserves form bounds",Bounds(form)==formBefore);
                Check($"Settings search {searchSize.Width} preserves navigation bounds",Bounds(navigation)==navigationBefore);
                var surface=(FrameworkElement)settingsWindow.FindName("SearchResultsSurface");
                var surfaceBounds=Bounds(surface);
                Check($"Settings search {searchSize.Width} results stay within form",overlay.Visibility==Visibility.Visible && surfaceBounds.Height>0 && formBefore.Contains(surfaceBounds));
            }
            settingsQuery.Text="zzzzzzzz-no-match"; root.UpdateLayout();
            Check($"Settings search {searchSize.Width} no-results hint uses overlay",overlay.Visibility==Visibility.Visible && ((FrameworkElement)settingsWindow.FindName("SearchResults")).Visibility==Visibility.Collapsed && Bounds(form)==formBefore);
            settingsQuery.Clear(); root.UpdateLayout();
            Check($"Settings search {searchSize.Width} clearing releases overlay",overlay.Visibility==Visibility.Collapsed && Bounds(form)==formBefore);
        }
        Check("Settings search has a stable text layout width", settingsQuery.Width == 300 && !settingsQuery.HasAnimatedProperties);
        settingsQuery.Text="màu";
        ((FrameworkElement)settingsWindow.Content).RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.Input.Mouse.PreviewMouseDownEvent});
        Pump(250);
        Check("Blank settings click clears results and collapses search",settingsQuery.Text=="" && settingsQuery.Visibility==Visibility.Collapsed && ((FrameworkElement)settingsWindow.FindName("SearchResults")).Visibility==Visibility.Collapsed);
        Check("Search blank hit policy preserves inputs",!SearchHitTest.IsBlank(settingsQuery,settingsWindow) && !SearchHitTest.IsBlank(widthControl,settingsWindow));
        Check("Unrelated visual is not an empty settings area",!SearchHitTest.IsBlank(new System.Windows.Controls.Border(),settingsWindow));
        settingsQuery.Text="test"; settingsQuery.Clear();
        var escapeSearch=new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
            System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(settingsWindow).EnsureHandle()),0,System.Windows.Input.Key.Escape){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent};
        typeof(SettingsWindow).GetMethod("RecordKey",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(settingsWindow,[settingsWindow,escapeSearch]);
        Pump(250);
        Check("Escape collapses empty settings search without cancel",escapeSearch.Handled && settingsQuery.Visibility==Visibility.Collapsed);
        Check("Separate RGB sliders removed", settingsWindow.FindName("RedChannel") == null);
        var picker = (ColorField)settingsWindow.FindName("ColorPicker");
        picker.SelectedColor = Colors.Magenta;
        Check("Color picker emits RGB hex", ((System.Windows.Controls.TextBox)settingsWindow.FindName("CustomAccent")).Text == "#FF00FF");
        Render(picker,280,200,Path.Combine(output,"color-picker.png"));
        Check("Scrollbar context menu disabled", (bool)((Style)app.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)]).Setters.OfType<Setter>().First(x => x.Property == System.Windows.Controls.ContextMenuService.IsEnabledProperty).Value == false);
        ThemeService.Apply(new AppSettings { Theme = ThemeOption.Light });
        preview.Settings.ShowMonitor = true; preview.Settings.EdgePosition = EdgePosition.Left; preview.Settings.Theme = ThemeOption.Light; preview.ApplySettings();
        Render((FrameworkElement)preview.Content, 380, 880, Path.Combine(output, "nepply-ivory.png"));
        preview.Settings.Theme = ThemeOption.Dark; preview.Settings.Accent = "Neon Cyan"; preview.ApplySettings();
        Render((FrameworkElement)preview.Content, 380, 880, Path.Combine(output, "nepply-neon.png"));
        ThemeService.ApplyAccessibility(preview.Settings,true,true);
        Render((FrameworkElement)preview.Content,380,880,Path.Combine(output,"nepply-system-contrast.png"));
        ThemeService.Apply(preview.Settings);
        iconVm.Icons.Clear(); foreach (var item in actualIcons) iconVm.Icons.Add(item);
        icons.DataContext = null; icons.DataContext = iconVm;
        Render((FrameworkElement)preview.Content, 380, 880, Path.Combine(output, desktopIntegration ? "nepply-desktop-actual.png" : "nepply-desktop-fixture.png"));
        var orderedVm = new DesktopIconViewModel(); var orderedSettings = new AppSettings();
        typeof(DesktopIconViewModel).GetField("_settings",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(orderedVm,orderedSettings);
        typeof(DesktopIconViewModel).GetField("_all",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(orderedVm,actualIcons.ToArray());
        if (actualIcons.Count >= 2)
        {
            Check("Internal icon reorder succeeds", orderedVm.Reorder(actualIcons[1].Path,actualIcons[0].Path));
            Check("Icon order stored without changing files", orderedSettings.IconOrder[0] == actualIcons[1].Path && orderedVm.Icons.Count == actualIcons.Count);
            Check("Foreign drag source rejected", !orderedVm.Reorder("foreign",actualIcons[0].Path));
            Check("Drop after target supported", orderedVm.Reorder(actualIcons[1].Path,actualIcons[0].Path,true) && Array.IndexOf(orderedSettings.IconOrder,actualIcons[1].Path) == Array.IndexOf(orderedSettings.IconOrder,actualIcons[0].Path)+1);
        }
        var oldFrame=demo with {MonotonicTimestamp=System.Diagnostics.Stopwatch.GetTimestamp()-System.Diagnostics.Stopwatch.Frequency*25};
        typeof(HardwareMonitorViewModel).GetField("_display",BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm,oldFrame);
        vm.SetActive(true); Pump(2200);
        Render((FrameworkElement)preview.Content,380,880,Path.Combine(output,"monitor-stale.png"));
        Check("Stale monitor warning appears from freshness timer",((FrameworkElement)monitorPanel.FindName("StaleNotice")).Visibility==Visibility.Visible);
        typeof(HardwareMonitorViewModel).GetField("_display",BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm,demo with {MonotonicTimestamp=System.Diagnostics.Stopwatch.GetTimestamp()});
        Pump(2200);
        Check("Fresh sample clears stale warning",((FrameworkElement)monitorPanel.FindName("StaleNotice")).Visibility==Visibility.Collapsed);
        vm.SetActive(false);
        preview.Cleanup(); settingsWindow.Close(); preview.Close();
        bindingListener.Flush();
        Check("UI bindings have no errors", string.IsNullOrWhiteSpace(bindingErrors.ToString()));
        PresentationTraceSources.DataBindingSource.Listeners.Remove(bindingListener);
        File.WriteAllText(Path.Combine(output, "binding-errors.txt"), bindingErrors.ToString());
        var animatedCard = new System.Windows.Controls.Border { Width = 100, Height = 80 };
        MetallicSurface.MotionEnabled = true;
        MetallicSurface.MaterialEnabled = true;
        MetallicSurface.NeonPalette = false;
        MetallicSurface.SetAccent(animatedCard, new SolidColorBrush(Colors.Cyan));
        var animationWindow = new Window { Content = animatedCard, Width = 120, Height = 100, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        animationWindow.Show();
        animationWindow.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
        Check("Metallic animation starts when visible", ((LinearGradientBrush)animatedCard.Background).RelativeTransform.HasAnimatedProperties);
        animationWindow.Hide();
        Check("Metallic animation stops when hidden", !((LinearGradientBrush)animatedCard.Background).RelativeTransform.HasAnimatedProperties);
        MetallicSurface.MotionEnabled = false; animationWindow.Show();
        Check("Static material disables animation", !((LinearGradientBrush)animatedCard.Background).RelativeTransform.HasAnimatedProperties);
        animationWindow.Close(); MetallicSurface.MotionEnabled = true;
        foreach (var edge in Enum.GetValues<EdgePosition>())
        {
            // Production hover lives on an inner icon chrome, not the panel transform.
            // Separate owners so a delayed panel clock cannot masquerade as a hover leak.
            // Bounded completion checks here measure lifecycle, not visible frame pacing.
            var transform = new TranslateTransform();
            var hoverTransform = new TranslateTransform();
            var hoverCard = new System.Windows.Controls.Border { Background = Brushes.Gray, RenderTransform = hoverTransform };
            var dock = new Window { Content = new System.Windows.Controls.Border { Background = Brushes.Black, RenderTransform = transform, Child = hoverCard }, Width = 320, Height = 360, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            bool motionEnabled=true;
            var motion = new AnimationService(transform,dock,new AppSettings { EdgePosition = edge }) { AnimationsEnabled=()=>motionEnabled };
            motion.HideInstantly(); motion.Show();
            if(edge==EdgePosition.Bottom)
            {
                motion.HideInstantly();
                Check("Bottom hidden transform points below panel",transform.X==0 && transform.Y==dock.Height);
                motion.Show();
            }
            Check($"{edge} reveal settles before independent hover", PumpUntil(() => motion.IsVisible && !motion.IsAnimating && !transform.HasAnimatedProperties && transform.X == 0 && transform.Y == 0, 1500));
            HoverLift.SetDistance(hoverCard,3);
            double hoverTarget = SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast ? -3 : 0;
            HoverLift.SetHovered(hoverCard,true);
            Check($"{edge} hover releases completed animation clock", PumpUntil(() => !hoverTransform.HasAnimatedProperties && hoverTransform.Y == hoverTarget, 1500));
            HoverLift.SetHovered(hoverCard,false); Pump(30);
            HoverLift.SetHovered(hoverCard,true);
            Check($"{edge} reversed hover releases completed animation clock", PumpUntil(() => !hoverTransform.HasAnimatedProperties && hoverTransform.Y == hoverTarget, 1500));
            Check($"{edge} hover does not alter panel transform", !transform.HasAnimatedProperties && transform.X == 0 && transform.Y == 0);
            HoverLift.SetEnabled(hoverCard,false);
            Check($"{edge} disabling hover resets translation",hoverTransform.Y==0 && !hoverTransform.HasAnimatedProperties);
            HoverLift.SetDistance(hoverCard,0);
            Check($"{edge} reveal settles", motion.IsVisible && !motion.IsAnimating && transform.X == 0 && transform.Y == 0);
            // This offscreen fixture verifies completion, not frame pacing.
            // WPF completion delivery can be delayed by renderer/dispatcher load;
            // observe the actual terminal state with a bounded deadline instead
            // of treating one fixed sleep as proof that it will never settle.
            var hideElapsed = Stopwatch.StartNew();
            motion.Hide();
            bool hideSettled = PumpUntil(() => !motion.IsVisible && !motion.IsAnimating && !dock.IsVisible, 1500);
            Check($"{edge} hide settles", hideSettled);
            Results.Add($"INFO {edge} offscreen hide completion: {hideElapsed.ElapsedMilliseconds} ms; not a visible animation performance measurement.");
            motion.Show(); Pump(50); motion.Hide(); Pump(50); motion.Show();
            Check($"{edge} reversed animation settles", PumpUntil(() => motion.IsVisible && !motion.IsAnimating && !transform.HasAnimatedProperties && transform.X == 0 && transform.Y == 0, 1500));
            int geometryVisibilityEvents=0, geometryHiddenEvents=0;
            motion.VisibilityChanged += _ => geometryVisibilityEvents++;
            dock.IsVisibleChanged += (_,_) => geometryHiddenEvents++;
            motion.Hide(); motion.Show(); Pump(40);
            geometryVisibilityEvents=0; geometryHiddenEvents=0;
            dock.Width=400; dock.Height=280;
            motion.RefreshGeometry(); Pump(380);
            Check($"{edge} geometry refresh preserves open window",dock.IsVisible && motion.IsVisible && !motion.IsAnimating && transform.X==0 && transform.Y==0 && !transform.HasAnimatedProperties);
            Check($"{edge} geometry refresh has no visibility churn",geometryVisibilityEvents==0 && geometryHiddenEvents==0);
            motionEnabled=false;
            motion.Hide();
            Check($"{edge} accessibility hide has no animation", !motion.IsAnimating && !dock.IsVisible && !transform.HasAnimatedProperties);
            motion.Show();
            Check($"{edge} accessibility reveal is immediate", motion.IsVisible && dock.IsVisible && !motion.IsAnimating && transform.X==0 && transform.Y==0);
            motionEnabled=true; motion.Hide(); Pump(40);
            motionEnabled=false; motion.RefreshMotionPreference();
            Check($"{edge} live preference settles pending hide",!motion.IsAnimating && !dock.IsVisible && !transform.HasAnimatedProperties);
            motion.Show(); Pump(300);
            Check($"{edge} canceled completion cannot hide new reveal",dock.IsVisible && motion.IsVisible && transform.X==0 && transform.Y==0);
            motion.Hide(); motionEnabled=true; motion.Show(); Pump(40);
            motionEnabled=false; motion.RefreshMotionPreference();
            Check($"{edge} live preference settles pending reveal",dock.IsVisible && !motion.IsAnimating && transform.X==0 && transform.Y==0 && !transform.HasAnimatedProperties);
            dock.Close();
        }
        var morphSurface=new System.Windows.Controls.Border {Background=Brushes.Black};
        var morphTransform=new TranslateTransform(); morphSurface.RenderTransform=morphTransform;
        var morphWindow=new Window {Content=morphSurface,Width=720,Height=320,Left=-10000,Top=-10000,ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None};
        bool morphEnabled=true;
        var morph=new AnimationService(morphTransform,morphWindow,new AppSettings {EdgePosition=EdgePosition.Top},morphSurface) {AnimationsEnabled=()=>morphEnabled};
        morph.HideInstantly(); morph.Show();
        bool sawSlide=PumpUntil(()=>morphTransform.Y<0 && morphTransform.Y>-320,1000);
        Check("Floating panel slides down without scaling text",morphSurface.Clip==null && sawSlide);
        morph.Hide(); Pump(30); morph.Show();
        Check("Island reversed morph settles without clipping or clocks",PumpUntil(()=>morph.IsVisible && !morph.IsAnimating && morphSurface.Clip==null && !morphTransform.HasAnimatedProperties && morphTransform.X==0 && morphTransform.Y==0,1500));
        morph.Hide(); PumpUntil(()=>!morph.IsAnimating,3000);
        Check("Island hide removes expanded window",!morphWindow.IsVisible && !morph.IsAnimating && morphSurface.Clip==null);
        morph.Show(); Pump(50); morphEnabled=false; morph.RefreshMotionPreference();
        Check("Island motion preference settles live morph",morphWindow.IsVisible && morphSurface.Clip==null && !morph.IsAnimating);
        morphWindow.Close();
        var lifecycle=new MainWindow(preview:true) {AllowPreviewIsland=true};
        var oldIslandSettings=System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"ShowCompactIsland\":true,\"PanelWidth\":420}")!;
        Check("Old capsule setting is ignored without losing panel preferences",oldIslandSettings.PanelWidth==420 && typeof(AppSettings).GetProperty("ShowCompactIsland")==null);
        lifecycle.Settings.EdgePosition=EdgePosition.Top; lifecycle.ApplySettings(); lifecycle.PositionWindow(); lifecycle.HidePanelInstantly();
        Check("Floating panel hides completely",!lifecycle.IsVisible);
        foreach(var edge in Enum.GetValues<EdgePosition>())
        {
            lifecycle.Settings.EdgePosition=edge; lifecycle.ApplySettings(); lifecycle.PositionWindow();
            var display=(Nepply.Helpers.DisplayScreen)typeof(MainWindow).GetField("_screen",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(lifecycle)!;
            // Geometry fixtures must not depend on the user's pointer or foreground app.
            lifecycle.ReadPointerContext=()=>new PointerContext(
                (int)((lifecycle.Left+lifecycle.Width/2)*display.Scale),
                (int)((lifecycle.Top+lifecycle.Height/2)*display.Scale), 10, 20, 1);
            var border=(System.Windows.Controls.Border)lifecycle.FindName("MainPanel");
            Check($"Floating {edge} rounds all four corners",border.CornerRadius==new CornerRadius(26));
            Check($"Floating {edge} stays inset within work area",lifecycle.Left*display.Scale>display.Left && lifecycle.Top*display.Scale>display.Top && (lifecycle.Left+lifecycle.Width)*display.Scale<display.Right+1 && (lifecycle.Top+lifecycle.Height)*display.Scale<display.Bottom+1);
            lifecycle.ShowPanel(); Pump(420);
            Check($"Floating {edge} opens",lifecycle.IsVisible);
            var transit=edge switch {EdgePosition.Left=>new Point(-6,lifecycle.ActualHeight/2),EdgePosition.Right=>new Point(lifecycle.ActualWidth+6,lifecycle.ActualHeight/2),EdgePosition.Bottom=>new Point(lifecycle.ActualWidth/2,lifecycle.ActualHeight+6),_=>new Point(lifecycle.ActualWidth/2,-6)};
            var transitScreen=lifecycle.PointToScreen(transit);
            var insideMethod=typeof(MainWindow).GetMethod("PointerInsidePanel",BindingFlags.Instance|BindingFlags.NonPublic)!;
            Check($"Floating {edge} keeps transit gap interactive",(bool)insideMethod.Invoke(lifecycle,[(int)transitScreen.X,(int)transitScreen.Y])!);
            var outside=edge switch {EdgePosition.Left=>new Point(lifecycle.ActualWidth+20,lifecycle.ActualHeight/2),EdgePosition.Right=>new Point(-20,lifecycle.ActualHeight/2),EdgePosition.Bottom=>new Point(lifecycle.ActualWidth/2,-20),_=>new Point(lifecycle.ActualWidth/2,lifecycle.ActualHeight+20)};
            var outsideScreen=lifecycle.PointToScreen(outside);
            Check($"Floating {edge} releases pointer outside panel",!(bool)insideMethod.Invoke(lifecycle,[(int)outsideScreen.X,(int)outsideScreen.Y])!);
            lifecycle.HidePanelInstantly();
            Check($"Floating {edge} hidden surface never holds mouse interaction",!(bool)insideMethod.Invoke(lifecycle,[(int)transitScreen.X,(int)transitScreen.Y])!);
            Check($"Floating {edge} hides with no compact tab",!lifecycle.IsVisible && !Application.Current.Windows.OfType<Window>().Any(w=>w.Title=="Nepply · Island"));
        }
        lifecycle.ShowPanel(); Pump(420);
        Check("Pointer fixture opens before hidden-cursor test",lifecycle.IsVisible);
        lifecycle.ReadPointerContext=()=>new PointerContext(0,0,10,uint.MaxValue,0);
        var edgeTick=typeof(MainWindow).GetMethod("EdgeTick",BindingFlags.Instance|BindingFlags.NonPublic)!;
        edgeTick.Invoke(lifecycle,[null,EventArgs.Empty]);
        Check("Hidden external cursor hides actual panel",!lifecycle.IsVisible);
        var suppressField=typeof(MainWindow).GetField("_suppress",BindingFlags.Instance|BindingFlags.NonPublic)!;
        suppressField.SetValue(lifecycle,1);
        lifecycle.ShowPanel(); Pump(420);
        edgeTick.Invoke(lifecycle,[null,EventArgs.Empty]);
        Check("Protected interaction keeps actual panel open",lifecycle.IsVisible);
        suppressField.SetValue(lifecycle,0);
        // Typing in another app hides the cursor too (Windows "hide pointer while typing").
        lifecycle.TogglePin();
        edgeTick.Invoke(lifecycle,[null,EventArgs.Empty]);
        Check("Pinned panel stays open when typing elsewhere hides the cursor",lifecycle.IsVisible && lifecycle.IsPinned);
        lifecycle.TogglePin();
        lifecycle.ReadPointerContext=()=>null;
        edgeTick.Invoke(lifecycle,[null,EventArgs.Empty]);
        Check("Unknown pointer does not forcibly hide actual panel",lifecycle.IsVisible);
        lifecycle.Cleanup(); lifecycle.Close();
        var dragRoot=new System.Windows.Controls.StackPanel();
        var dragInput=new System.Windows.Controls.Slider(); var dragLabel=new System.Windows.Controls.TextBlock {Text="Drag"};
        dragRoot.Children.Add(dragInput); dragRoot.Children.Add(dragLabel);
        Check("Widget drag does not steal slider gesture",!WidgetDrag.CanStartFrom(dragRoot,dragInput));
        Check("Widget drag starts from non-interactive label",WidgetDrag.CanStartFrom(dragRoot,dragLabel));
        Check("Widget drag rejects unrelated source",!WidgetDrag.CanStartFrom(dragRoot,new System.Windows.Controls.TextBlock()));
        Check("Grid preview shifts intervening tiles forward",Enumerable.Range(0,5).Select(i=>IconGridPanel.PreviewIndex(i,1,5,5)).SequenceEqual(new[]{0,4,1,2,3}));
        Check("Grid preview shifts intervening tiles backward",Enumerable.Range(0,5).Select(i=>IconGridPanel.PreviewIndex(i,4,1,5)).SequenceEqual(new[]{0,2,3,4,1}));
        Check("Grid preview invalid source leaves order untouched",IconGridPanel.PreviewIndex(2,-1,3,5)==2);
        var reflow=new GridReflow(); var tile=new System.Windows.Controls.Border {DataContext=new object()};
        tile.Measure(new Size(80,90)); reflow.Arrange(tile,new Rect(0,0,80,90),false);
        reflow.Arrange(tile,new Rect(90,0,80,90),true); Pump(260);
        Check("Grid reflow releases completed clocks",tile.RenderTransform is TranslateTransform settled && !settled.HasAnimatedProperties && settled.X==0 && settled.Y==0);
        reflow.Arrange(tile,new Rect(0,90,80,90),true); reflow.Clear();
        Check("Grid reflow cleanup drops retained containers",reflow.Count==0 && !tile.RenderTransform.HasAnimatedProperties);
        reflow.Arrange(tile,new Rect(0,0,80,90),false); tile.DataContext=new object(); reflow.Arrange(tile,new Rect(80,90,80,90),true);
        Check("Grid reflow never animates recycled item identity",!tile.RenderTransform.HasAnimatedProperties);
        reflow.Retain(new HashSet<UIElement>()); Check("Grid reflow evicts unrealized containers",reflow.Count==0);
        Results.Add($"RESULT: {_passed} passed; {_failures} failed. Native collector: {(args.Contains("--native") ? "enabled" : "disabled")}. UI metrics are illustrative; Desktop inventory: {(desktopIntegration ? "real Shell" : "fixture only; integration excluded")}.");
        File.WriteAllLines(Path.Combine(output, "verification.txt"), Results);
        foreach (var line in Results) Console.WriteLine(line);
        return _failures == 0 ? 0 : 1;
    }
    private static void VerifyPumpDeadline()
    {
        Pump(1); // Complete WPF's first dispatcher/startup pass before measuring the deadline.
        bool running = true;
        int callbacks = 0;
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        Action busy = null!;
        busy = () =>
        {
            if (!running) return;
            callbacks++;
            Thread.Sleep(2);
            dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, busy);
        };
        dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, busy);
        var elapsed = Stopwatch.StartNew();
        try { Pump(40); }
        finally { running = false; }
        Check("Fixture pump deadline survives a continuously busy render queue", callbacks > 0 && elapsed.ElapsedMilliseconds < 2000);
        Pump(1); // Drain the last no-op callback without retaining its closure.
    }
    private static void Pump(int milliseconds)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        // The deadline must not depend on dispatcher timers in the dispatcher under test.
        // DispatcherFrame.Continue wakes its owning dispatcher when set from another thread.
        using var deadline = new System.Threading.Timer(_ => frame.Continue = false,
            null, milliseconds, System.Threading.Timeout.Infinite);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
    private static bool PumpUntil(Func<bool> condition,int timeoutMs)
    {
        var elapsed=Stopwatch.StartNew();
        while(!condition() && elapsed.ElapsedMilliseconds<timeoutMs) Pump(10);
        return condition();
    }
    private static void Render(FrameworkElement element, int width, int height, string destination)
    {
        element.Width = width; element.Height = height;
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(destination); encoder.Save(file);
    }
    private static T? FindVisual<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T target) return target;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            if (FindVisual<T>(VisualTreeHelper.GetChild(parent, i)) is { } child) return child;
        return null;
    }
}
