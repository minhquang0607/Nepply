using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Nepply;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;

internal static class DesktopPipelineTests
{
    internal static int Run(string output,string executable,bool explorerRecovery=false)
    {
        string desktop=Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        string original=Path.Combine(desktop,"Nepply-QA-"+Guid.NewGuid().ToString("N"));
        string renamed=original+"-đổi-日本語";
        if(!File.Exists(executable) || Directory.Exists(original) || Directory.Exists(renamed))
            throw new InvalidOperationException("Invalid helper or occupied fixture path.");
        var app=new App(testHost:true);app.InitializeComponent();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        using var catalog=new DesktopIconService(executable);
        var window=new MainWindow(preview:true,icons:catalog,monitor:new QuietMonitor()) {ShowActivated=false,ShowInTaskbar=false};
        DesktopWatcherService? watcher=null;
        DesktopUpdateCoordinator? coordinator=null;
        var checks=new List<object>();
        bool created=false,passed=true;
        string? error=null,cleanupError=null;
        try
        {
            window.Settings.ShowMonitor=false;
            window.Show();
            var list=(ListBox)((IconPanel)window.FindName("IconPanelView")).FindName("IconList");
            if(explorerRecovery) coordinator=new DesktopUpdateCoordinator(window,catalog);
            else watcher=new DesktopWatcherService(catalog);
            bool Has(string path)=>list.Items.OfType<DesktopIcon>().Any(item=>string.Equals(item.Path,path,StringComparison.OrdinalIgnoreCase));
            void Expect(string name,Func<bool> condition)
            {
                var clock=Stopwatch.StartNew();
                while(!condition() && clock.Elapsed<TimeSpan.FromSeconds(35)) Pump();
                bool ok=condition() && catalog.ScanState.Error==null;
                checks.Add(new {Name=name,Passed=ok,Seconds=clock.Elapsed.TotalSeconds});
                Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name} ({clock.Elapsed.TotalSeconds:F2}s)");
                if(!ok) throw new InvalidOperationException(name+": "+catalog.ScanFailureDiagnostic);
            }
            Expect("Baseline reaches WPF without manual refresh",()=>catalog.HasScanned && !catalog.ScanState.IsScanning && list.Items.Count==catalog.GetIcons().Count);
            if(explorerRecovery)
            {
                File.WriteAllText(Path.Combine(output,"ready-for-explorer-restart"),Environment.ProcessId.ToString());
                var wait=Stopwatch.StartNew();
                while(!File.Exists(Path.Combine(output,"explorer-restarted")) && wait.Elapsed<TimeSpan.FromMinutes(3)) Pump();
                if(!File.Exists(Path.Combine(output,"explorer-restarted"))) throw new TimeoutException("Explorer restart was not acknowledged.");
            }
            Directory.CreateDirectory(original);created=true;
            Expect("Created folder reaches panel via watcher and helper",()=>Has(original));
            Directory.Move(original,renamed);
            Expect("Unicode rename removes old identity and adds new identity",()=>!Has(original) && Has(renamed));
            RemoveOwnedEmpty(renamed,desktop);
            Expect("Deleted folder disappears from panel",()=>!Has(original) && !Has(renamed));
        }
        catch(Exception ex) {passed=false;error=ex.ToString();}
        finally
        {
            try {coordinator?.Dispose();}
            catch(Exception ex) {passed=false;cleanupError=ex.ToString();}
            watcher?.Dispose();
            if(watcher!=null)
                try {watcher.Completion.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();}
                catch(Exception ex) {passed=false;cleanupError=ex.ToString();}
            try {window.Cleanup();}
            catch(Exception ex) {passed=false;cleanupError=(cleanupError??"")+ex;}
            try {window.Close();app.MainWindow=null;}
            catch(Exception ex) {passed=false;cleanupError=(cleanupError??"")+ex;}
            if(created)
                foreach(string path in new[]{original,renamed})
                    try {if(Directory.Exists(path)) RemoveOwnedEmpty(path,desktop);}
                    catch(Exception ex) {passed=false;cleanupError=(cleanupError??"")+ex;}
        }
        bool cleaned=!Directory.Exists(original) && !Directory.Exists(renamed);
        passed &= cleaned;
        File.WriteAllText(Path.Combine(output,"desktop-pipeline.json"),JsonSerializer.Serialize(new
        {
            Passed=passed,ExplorerRecovery=explorerRecovery,Cleaned=cleaned,Original=original,Renamed=renamed,Checks=checks,Error=error,CleanupError=cleanupError,
            Scope="One user-authorized empty Desktop folder create/rename/delete; real DesktopWatcherService, published Shell helper IPC, catalog and WPF binding; preview host avoids hiding Desktop. No manual refresh. Not a 500-item burst or Explorer restart test."
        },new JsonSerializerOptions {WriteIndented=true}));
        return passed ? 0 : 1;
    }
    private static void RemoveOwnedEmpty(string path,string desktop)
    {
        if(!string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),desktop,StringComparison.OrdinalIgnoreCase)
            || (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)
            throw new IOException("Refusing unexpected cleanup target.");
        Directory.Delete(path,recursive:false); // Never delete content added by anyone else.
    }
    private static void Pump()
    {
        var frame=new DispatcherFrame();
        var timer=new DispatcherTimer(DispatcherPriority.ApplicationIdle) {Interval=TimeSpan.FromMilliseconds(100)};
        EventHandler handler=(_,_)=>frame.Continue=false;
        timer.Tick+=handler;timer.Start();
        try {Dispatcher.PushFrame(frame);}
        finally {timer.Stop();timer.Tick-=handler;}
    }
    private sealed class QuietMonitor : Nepply.Services.Telemetry.IHardwareMonitor
    {
        public event Action<HardwareInfoSnapshot>? HardwareUpdated {add {} remove {}}
        public HardwareInfoSnapshot GetSnapshot()=>HardwareInfoSnapshot.Empty;
        public void Configure(AppSettings settings,bool visible) {}
        public void Start() {}
        public void Dispose() {}
    }
}
