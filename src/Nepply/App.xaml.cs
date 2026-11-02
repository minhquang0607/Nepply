using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Nepply.Services;
namespace Nepply;

public partial class App : Application
{
    private readonly bool _testHost;
    public App() { }
    public App(bool testHost) : this() => _testHost = testHost;
    private TaskbarIcon? _tray;
    private Modules.Updates.UpdateService? _updateService;
    private Modules.Updates.BackgroundUpdateCheck? _updateCheck;
    private Action? _detachTrayLanguage;
    private Icon? _icon;
    private Mutex? _instance;
    private Mutex? _installerGuard;
    private EventWaitHandle? _activate;
    private RegisteredWaitHandle? _activationWait;
    private bool _ownsInstance;
    private bool _preview;
    private bool _fatalUiError;
    protected override void OnStartup(StartupEventArgs e)
    {
        // The renderer test harness pumps WPF but must not launch the tray or modify Desktop state.
        if (_testHost) { base.OnStartup(e); return; }
        if (e.Args.Length == 1 && e.Args[0] == "--restore-desktop")
        {
            // Keep the owned lease on timeout; never let a blocked native restore
            // keep the recovery process alive indefinitely after managed startup.
            using var deadline = new Timer(_ => Environment.Exit(4), null, TimeSpan.FromSeconds(20), Timeout.InfiniteTimeSpan);
            // Uninstaller recovery runs before any UI, tray or sensor initialization.
            // Refuse to race a live app that still owns the user's Desktop lease.
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                using var recoveryMutex = new Mutex(false, ProductIdentity.InstancePrefix + (identity.User?.Value ?? Environment.UserName));
                Shutdown((int)DesktopRecovery.Run(
                    () => Helpers.InstanceOwnership.TryAcquire(recoveryMutex),
                    DesktopVisibilityLease.RestorePreviousSession, recoveryMutex.ReleaseMutex));
            }
            catch (Exception ex) { AppLogger.Error(ex, "Restore Desktop before uninstall"); Shutdown(1); }
            return;
        }
        if (e.Args.Length == 7 && e.Args[0] == "--desktop-host" && int.TryParse(e.Args[6], out var desktopParentId) && desktopParentId > 0)
        {
            try { DesktopScanProcess.RunHost(e.Args[1], e.Args[2] == "1", e.Args[3] == "1", e.Args[4] == "1", e.Args[5] == "raw", desktopParentId); }
            catch (Exception ex) { AppLogger.Error(ex, "Desktop host"); Shutdown(1); return; }
            Shutdown(); return;
        }
        if (e.Args.Length == 3 && e.Args[0] == "--sensor-host" && int.TryParse(e.Args[2], out var parentId))
        {
            try { Services.Telemetry.SensorProcessProvider.RunHost(e.Args[1], parentId); }
            catch (Exception ex) { AppLogger.Error(ex, "Sensor host"); Shutdown(1); return; }
            Shutdown(); return;
        }
        // Malformed/legacy helper requests must never fall through into a normal desktop session.
        if (e.Args.Any(arg => arg is "--desktop-host" or "--sensor-host" or "--restore-desktop")) { Shutdown(2); return; }
        bool smokeTest = e.Args.Contains("--smoke-test");
        bool islandPreview = e.Args.Contains("--preview-island");
        _preview = e.Args.Contains("--preview") || smokeTest || islandPreview;
        if (_preview)
            Models.AppSettings.UseTestSettingsStore(Path.Combine(Path.GetTempPath(), "Nepply-preview", Guid.NewGuid().ToString("N"), "settings.json"));
        if (!_preview)
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            string sid = identity.User?.Value ?? Environment.UserName;
            // Publish the activation signal before acquiring ownership. A second
            // launch can signal immediately, even before the owner starts waiting.
            _activate = new EventWaitHandle(false, EventResetMode.AutoReset, ProductIdentity.ActivationPrefix + sid);
            _instance = new Mutex(false, ProductIdentity.InstancePrefix + sid);
            _ownsInstance = Helpers.InstanceOwnership.TryAcquire(_instance);
            if (!_ownsInstance)
            {
                try { _activate.Set(); }
                catch (Exception ex) { AppLogger.Error(ex, "Activate running instance"); }
                Shutdown(); return;
            }
            _installerGuard = new Mutex(false, Helpers.InstanceOwnership.InstallerMutexName);
            try { DesktopVisibilityLease.RestorePreviousSession(); }
            catch (Exception ex) { AppLogger.Error(ex, "Recover previous Desktop session"); }
            _activationWait = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) => Dispatcher.BeginInvoke(() =>
            { if (MainWindow is MainWindow main) main.OpenForInteraction(); }), null, Timeout.Infinite, false);
        }
        DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Error(args.Exception, "UI error");
            args.Handled = true;
            // Recoverable operation errors belong at call sites; unknown UI
            // failures must not restart a possibly inconsistent timer loop.
            if (_fatalUiError) return;
            _fatalUiError = true;
            Helpers.CleanupSequence.Run(("UI failure cleanup", () => (MainWindow as MainWindow)?.Cleanup()));
            try
            {
                MessageBox.Show(ErrorMessage("FatalUiError", "Nepply encountered an error and must close to avoid continuing in an unstable state. Unsaved changes will not be saved automatically. Details were logged; you can reopen the app."), "Nepply", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { Shutdown(1); }
        };
        TaskScheduler.UnobservedTaskException += (_, args) => { AppLogger.Error(args.Exception, "Background task"); args.SetObserved(); };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        { if (args.ExceptionObject is Exception ex) AppLogger.Error(ex, "Fatal error"); AppLogger.FlushAndShutdown(); };
        base.OnStartup(e);
        SessionEnding += (_, _) => (MainWindow as MainWindow)?.Cleanup();
        MainWindow window;
        try
        {
            var services = Modules.WindowServices.Create(_preview, smokeTest);
            try { window = new MainWindow(services, _preview, smokeTest); }
            catch { services.Dispose(); throw; }
            if (islandPreview)
            {
                window.AllowPreviewIsland = true;
                window.Settings.EdgePosition = Models.EdgePosition.Top;
            }
            MainWindow = window;
            // Preview is an explicit interactive test surface, not the hidden production dock.
            if (_preview && e.Args.Contains("--show"))
            {
                window.ShowInTaskbar = true;
                window.TogglePin();
            }
            SetupTray(window);
            window.Show();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Initialize application UI");
            Helpers.CleanupSequence.Run(("Failed startup", () => (MainWindow as MainWindow)?.Cleanup()));
            try
            {
                MessageBox.Show(ErrorMessage("StartupError", "Nepply could not start. You can reopen the app; error details were logged."),
                    "Nepply", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--show")) Dispatcher.BeginInvoke(window.OpenForInteraction);
        if (smokeTest)
        {
            var started = System.Diagnostics.Stopwatch.StartNew();
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var cpuBefore = process.TotalProcessorTime;
            var timeout = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            timeout.Tick += async (_, _) =>
            {
                var desktopState = window.CurrentDesktopScanState;
                bool desktopTerminal = !desktopState.IsScanning && (desktopState.HasSnapshot || desktopState.Error != null);
                // The Desktop helper has a 25-second deadline. Allow its terminal
                // result to arrive before reporting an empty catalog as a success.
                if (!desktopTerminal && started.Elapsed < TimeSpan.FromSeconds(35))
                {
                    timeout.Interval = TimeSpan.FromSeconds(1);
                    return;
                }
                timeout.Stop();
                var reveals = new[] { await window.MeasureRevealAsync(), await window.MeasureRevealAsync(), await window.MeasureRevealAsync() };
                process.Refresh();
                var snapshot = window.CurrentSnapshot;
                bool passed = desktopTerminal && desktopState.HasSnapshot && desktopState.Error == null && snapshot.Sequence > 0;
                var result = new
                {
                    Completed = true,
                    Passed = passed,
                    Seconds = started.Elapsed.TotalSeconds,
                    DesktopItems = window.DesktopItemCount,
                    DesktopScan = desktopState,
                    DesktopScanTimedOut = !desktopTerminal,
                    DesktopScanDiagnostic = window.DesktopScanDiagnostic,
                    Frames = snapshot.Sequence,
                    CpuLoad = snapshot.CpuLoad,
                    RamTotalGiB = snapshot.RamTotalGB,
                    WorkingSetMiB = process.WorkingSet64 / 1048576d,
                    PrivateMiB = process.PrivateMemorySize64 / 1048576d,
                    ProcessCpuSeconds = (process.TotalProcessorTime - cpuBefore).TotalSeconds,
                    FirstRevealMs = window.SmokeFirstRevealMs,
                    RevealBlockedMs = reveals.Select(reveal => reveal.BlockedMs).ToArray(),
                    RevealSlowestFrameMs = reveals.Select(reveal => reveal.SlowestFrameMs).ToArray(),
                    RevealFrames = reveals.Select(reveal => reveal.Frames).ToArray(),
                    Note = "Startup included; Windows metrics only; no desktop visibility or autostart modifications; not a long-run benchmark."
                };
                var outputIndex = Array.IndexOf(e.Args, "--smoke-output");
                if (outputIndex >= 0 && outputIndex + 1 < e.Args.Length)
                {
                    var path = Path.GetFullPath(e.Args[outputIndex + 1]);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                window.Cleanup(); Shutdown(passed ? 0 : 2);
            };
            timeout.Start();
        }
    }
    private void SetupTray(MainWindow window)
    {
        using var stream = GetResourceStream(new Uri("pack://application:,,,/Nepply;component/Resources/icon.ico"))!.Stream;
        using var temporary = new Icon(stream);
        _icon = (Icon)temporary.Clone();
        _tray = new TaskbarIcon { Icon = _icon };
        _detachTrayLanguage = AttachTrayLanguage(_tray);
        _tray.ContextMenu = new Views.TrayMenu(window, OpenLogDirectory,
            () => { if (!window.ConfirmExit()) return; window.Cleanup(); Shutdown(); });
        _tray.TrayMouseDoubleClick += (_, _) => window.OpenForInteraction();
        // Preview and smoke sessions must stay off the network.
        if (_preview || ProcessPackageIdentity.IsPackaged) return;
        _tray.TrayBalloonTipClicked += (_, _) => window.OpenUpdateSettings();
        _updateService = new Modules.Updates.UpdateService();
        _updateCheck = new Modules.Updates.BackgroundUpdateCheck(() => window.Settings.AutoCheckUpdates, _updateService.CheckAsync, release =>
            _tray.ShowBalloonTip("Nepply", LocalizationService.Render(UserMessage.FromKey("UpdateAutoFound", release.Tag.TrimStart('v'))), BalloonIcon.Info));
        _ = _updateCheck.RunAsync();
    }
    internal static Action AttachTrayLanguage(TaskbarIcon tray)
    {
        void Refresh() => tray.ToolTipText = LocalizationService.Get("TrayTooltip");
        Refresh();
        LocalizationService.Changed += Refresh;
        return () => LocalizationService.Changed -= Refresh;
    }
    private static void OpenLogDirectory()
    {
        try
        {
            Directory.CreateDirectory(AppLogger.LogDirectory);
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppLogger.LogDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Open log directory");
            MessageBox.Show(ErrorMessage("OpenLogsError", "Could not open the log folder. You can try again later."), "Nepply", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    // Keep this guard outside LocalizationService: its type initializer itself may fail.
    // Error reporting must not depend on successfully loading language resources.
    internal static string ErrorMessage(string key, string fallback, Func<string, string>? lookup = null)
    {
        try
        {
            string text = lookup != null ? lookup(key) : LocalizationService.Get(key);
            return string.IsNullOrWhiteSpace(text) || text == key ? fallback : text;
        }
        catch (Exception) { return fallback; }
    }
    private void Application_Exit(object sender, ExitEventArgs e)
    {
        Helpers.CleanupSequence.Run(
            ("Main window", () => (MainWindow as MainWindow)?.Cleanup()),
            ("Update check", () => { _updateCheck?.Dispose(); _updateService?.Dispose(); }
        ),
            ("Tray language", () => { _detachTrayLanguage?.Invoke(); _detachTrayLanguage = null; }
        ),
            ("Tray", () => _tray?.Dispose()),
            ("Tray icon", () => _icon?.Dispose()),
            ("Activation wait", () => _activationWait?.Unregister(null)),
            ("Activation event", () => _activate?.Dispose()),
            ("Instance ownership", () => { if (_ownsInstance) _instance?.ReleaseMutex(); }
        ),
            ("Instance handle", () => _instance?.Dispose()),
            ("Installer guard", () => _installerGuard?.Dispose()));
        AppLogger.FlushAndShutdown();
    }
}
