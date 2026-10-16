using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using Nepply;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;

internal static class UiSoakTests
{
    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process,uint flags);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    internal static int Run(string output,int minutes,string profile="normal")
    {
        if(minutes is < 1 or > 480) throw new ArgumentOutOfRangeException(nameof(minutes));
        if(profile is not ("normal" or "active" or "static" or "no-monitor" or "no-sweep" or "no-glow" or "cache-readings" or "split-glow" or "opaque" or "visible-active" or "visible-opaque" or "visible-no-sweep" or "visible-no-glow")) throw new ArgumentException("Unknown soak profile.",nameof(profile));
        bool visibleProfile=profile.StartsWith("visible-",StringComparison.Ordinal);
        var app=new App(testHost:true);app.InitializeComponent();
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        using var monitor=new HardwareMonitorService();
        var items=Enumerable.Range(0,500).Select(i=>new DesktopIcon {Name=$"Mẫu {i:D3}",Path=$"soak-fixture-{i}"}).ToList();
        using var catalog=new DesktopIconService(_=>Task.FromResult(items));
        catalog.ScanDesktopIconsAsync().GetAwaiter().GetResult();
        var window=new MainWindow(preview:true,icons:catalog,monitor:monitor)
            {ShowActivated=false,ShowInTaskbar=false,Left=-10000,Top=-10000};
        using var process=Process.GetCurrentProcess();
        uint uiThreadId=GetCurrentThreadId();
        using var log=new StreamWriter(Path.Combine(output,"ui-soak-samples.jsonl"),append:false) {AutoFlush=true};
        var clock=Stopwatch.StartNew();
        var timer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(30)};
        bool active=true;
        int samples=0;
        var experimentalCards=new List<Border>();
        bool experimentValid=true;
        Exception? failure=null;
        var frame=new DispatcherFrame();
        void Sample()
        {
            if(profile=="split-glow") experimentValid &= experimentalCards.Count>0 && experimentalCards.All(card=>card.Effect==null);
            process.Refresh();
            var threadCpu=new List<object>();
            foreach(ProcessThread thread in process.Threads)
            {
                using(thread)
                {
                    try { threadCpu.Add(new {Id=thread.Id,CpuSeconds=thread.TotalProcessorTime.TotalSeconds}); }
                    // A worker can exit between enumeration and querying its CPU time.
                    catch(System.ComponentModel.Win32Exception) { }
                    catch(InvalidOperationException) { }
                }
            }
            log.WriteLine(JsonSerializer.Serialize(new {Seconds=clock.Elapsed.TotalSeconds,Active=active,
                ManagedBytes=GC.GetTotalMemory(false),process.PrivateMemorySize64,process.WorkingSet64,
                process.HandleCount,Threads=process.Threads.Count,CpuSeconds=process.TotalProcessorTime.TotalSeconds,
                Gdi=GetGuiResources(process.Handle,0),User=GetGuiResources(process.Handle,1),
                monitor.WindowsSampleCount,Sequence=monitor.GetSnapshot().Sequence,ExperimentValid=experimentValid,
                window.IsVisible,window.IsMouseOver,window.IsActive,window.Left,window.Top,
                UiThreadId=uiThreadId,ThreadCpu=threadCpu}));
            samples++;
        }
        void SetActive(bool value)
        {
            active=value;
            if(active) {window.Show();if(!visibleProfile) {window.Left=-10000;window.Top=-10000;}} else window.Hide();
            ((MonitorPanel)window.FindName("MonitorPanelView")).SetActive(active);
            monitor.Configure(window.Settings,active);
        }
        EventHandler tick=(_,_)=>
        {
            try
            {
                Sample();
                Console.WriteLine($"UI soak {clock.Elapsed.TotalMinutes:F1}/{minutes} min; active={active}; handles={process.HandleCount}");
                if(clock.Elapsed>=TimeSpan.FromMinutes(minutes)) {frame.Continue=false;return;}
                // Two-minute smoke includes both states; long runs alternate every five minutes.
                if(profile=="normal") SetActive(((int)clock.Elapsed.TotalSeconds/(minutes<=2 ? 30 : 300))%2==0);
            }
            catch(Exception ex) {failure=ex;frame.Continue=false;}
        };
        try
        {
            window.Settings.HardwareSensorsEnabled=false;
            window.Settings.ShowMonitor=profile!="no-monitor";window.Settings.SuspendMonitorWhenHidden=true;
            if(profile=="static") window.Settings.Motion=MotionMode.Off;
            if(profile is "no-sweep" or "visible-no-sweep") window.Settings.DynamicMaterial=false;
            if(profile is "no-glow" or "visible-no-glow") window.Settings.GlowStrength=0;
            if(profile is "opaque" or "visible-opaque") {window.AllowsTransparency=false;window.Background=(Brush)app.Resources["CardBrush"];}
            window.Show();if(!visibleProfile) {window.Left=-10000;window.Top=-10000;}
            if(profile=="cache-readings")
                foreach(var reading in Descendants(window).OfType<MetricReadingView>())
                    reading.CacheMode=new BitmapCache {EnableClearType=true,SnapsToDevicePixels=true};
            if(profile=="split-glow")
                foreach(var card in Descendants(window).OfType<Border>().Where(b=>b.Child is MetricReadingView).ToArray())
                {
                    if(card.Parent is not Panel parent || card.Effect==null) continue;
                    int index=parent.Children.IndexOf(card);
                    var layer=new Grid {Margin=card.Margin,Height=card.Height};
                    var glow=new Border {CornerRadius=card.CornerRadius,Background=(Brush)app.Resources["CardBrush"],
                        Effect=card.Effect,IsHitTestVisible=false};
                    // Existing MetallicSurface lifecycle handlers may reapply the effect during reparenting.
                    card.Loaded+=(_,_)=>card.Effect=null;
                    card.IsVisibleChanged+=(_,_)=> {if(card.IsVisible) card.Effect=null;};
                    parent.Children.RemoveAt(index);card.Margin=new Thickness(0);card.Effect=null;
                    layer.Children.Add(glow);layer.Children.Add(card);parent.Children.Insert(index,layer);
                    card.Effect=null;experimentalCards.Add(card);
                }
            SetActive(true);monitor.Start();Sample();
            timer.Tick+=tick;timer.Start();Dispatcher.PushFrame(frame);
        }
        finally
        {
            timer.Stop();timer.Tick-=tick;
            window.Cleanup();window.Close();app.MainWindow=null;monitor.Dispose();
        }
        bool stopped=SpinWait.SpinUntil(()=>monitor.Completion.IsCompleted,10000) && monitor.Completion.IsCompletedSuccessfully;
        bool completed=failure==null && stopped && experimentValid && clock.Elapsed>=TimeSpan.FromMinutes(minutes);
        File.WriteAllText(Path.Combine(output,"ui-soak-summary.json"),JsonSerializer.Serialize(new
        {
            Completed=completed,Profile=profile,RenderingTier=System.Windows.Media.RenderCapability.Tier>>16,
            ExperimentValid=experimentValid,ExperimentalCards=experimentalCards.Count,
            window.Width,window.Height,window.AllowsTransparency,
            VisibleProfile=visibleProfile,
            RequestedMinutes=minutes,Seconds=clock.Elapsed.TotalSeconds,Samples=samples,
            LoopsStopped=stopped,Error=failure?.ToString(),
            Scope=(visibleProfile ? "Onscreen" : "Offscreen")+" persistent WPF panel, 500 fixture items, real Windows metrics; no native sensors, Shell extraction, forced GC or automated mouse interaction. CPU measurement is not GPU profiling. Completion is not a leak verdict; inspect time series."
        },new JsonSerializerOptions {WriteIndented=true}));
        return completed ? 0 : 1;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);
            yield return child;
            foreach(var descendant in Descendants(child)) yield return descendant;
        }
    }
}
