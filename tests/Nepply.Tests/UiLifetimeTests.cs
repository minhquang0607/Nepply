using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Nepply;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;

internal static class UiLifetimeTests
{
    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process,uint flags);

    internal static int Run(string output)
    {
        var app=new App(testHost:true);app.InitializeComponent();
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        var references=new List<WeakReference>();
        var samples=new List<object>();
        using var process=Process.GetCurrentProcess();
        for(int i=0;i<24;i++)
        {
            references.Add(Cycle(app));
            if((i+1)%4!=0) continue;
            DrainAndCollect();process.Refresh();
            int alive=references.Count(reference=>reference.IsAlive);
            samples.Add(new {Cycles=i+1,AliveWindows=alive,ManagedBytes=GC.GetTotalMemory(false),
                process.PrivateMemorySize64,process.HandleCount,Threads=process.Threads.Count,
                Gdi=GetGuiResources(process.Handle,0),User=GetGuiResources(process.Handle,1)});
            Console.WriteLine($"Cycles {i+1}/24: {alive} closed windows still alive; handles {process.HandleCount}");
        }
        DrainAndCollect();
        bool collected=references.All(reference=>!reference.IsAlive);
        File.WriteAllText(Path.Combine(output,"ui-lifetime.json"),JsonSerializer.Serialize(new
        {
            Passed=collected,ClosedWindowsCollected=collected,Cycles=references.Count,Samples=samples,
            Scope="Repeated offscreen WPF MainWindow create/show/hide/close with 500 fixture items. Forced GC is diagnostic only, not production behavior or an idle/long-duration benchmark. No native sensor or real Desktop modification."
        },new JsonSerializerOptions {WriteIndented=true}));
        return collected ? 0 : 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Cycle(Application app)
    {
        var items=Enumerable.Range(0,500).Select(i=>new DesktopIcon {Name=$"Mẫu {i:D3}",Path=$"fixture-{i}"}).ToList();
        using var catalog=new DesktopIconService(_=>Task.FromResult(items));
        catalog.ScanDesktopIconsAsync().GetAwaiter().GetResult();
        var window=new MainWindow(preview:true,icons:catalog,monitor:new QuietMonitor())
            {ShowActivated=false,ShowInTaskbar=false,Left=-10000,Top=-10000};
        try
        {
            window.Show();window.Left=-10000;window.Top=-10000;Pump();
            window.UpdateLayout();window.Hide();Pump();window.Show();Pump();
            return new WeakReference(window);
        }
        finally {window.Cleanup();window.Close();app.MainWindow=null;}
    }

    private static void DrainAndCollect()
    {
        for(int i=0;i<3;i++) {Pump();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
    }
    private static void Pump()
    {
        var frame=new DispatcherFrame();
        var timer=new DispatcherTimer(DispatcherPriority.ApplicationIdle) {Interval=TimeSpan.FromMilliseconds(40)};
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
