using Nepply.Models;
using Nepply.Services;
using Nepply.Services.Telemetry;

internal static class SamplingLifecycleTests
{
    private sealed class FakeProvider : IHardwareProvider
    {
        public string Name => "Lifecycle fixture (no hardware access)";
        internal int Reads, Disposals;
        public HardwareReading Read(AppSettings settings) { Interlocked.Increment(ref Reads); return HardwareReading.Empty; }
        public void Dispose() => Interlocked.Increment(ref Disposals);
    }
    internal static void Run(Action<string,bool> check)
    {
        NetworkLifecycle(check);
        using(var wake=new SamplingWake())
        using(var shutdown=new CancellationTokenSource())
        {
            var waiting=wake.WaitAsync(Timeout.Infinite,shutdown.Token);
            check("Disabled sampling waits for an explicit signal",!waiting.IsCompleted);
            wake.Signal();
            check("Configuration wakes sleeping lane",waiting.Wait(1000) && waiting.Result);
            wake.Signal(); wake.Signal(); wake.ConsumePending();
            var next=wake.WaitAsync(Timeout.Infinite,shutdown.Token);
            check("Configuration burst retains at most one pending wake",!next.IsCompleted);
            shutdown.Cancel();
            try { next.GetAwaiter().GetResult(); } catch(OperationCanceledException) { }
            check("Shutdown cancels infinite sampling wait",next.IsCanceled);
        }
        var fake=new FakeProvider(); int created=0;
        using var monitor=new HardwareMonitorService(()=>{Interlocked.Increment(ref created);return fake;},1000);
        var settings=new AppSettings {ShowMonitor=false,HardwareSensorsEnabled=true,EcoMode=false,MonitorUpdateIntervalMs=1000};
        monitor.Configure(settings,false); monitor.Start();
        Thread.Sleep(150);
        check("Disabled startup opens no native provider and reads no Windows metrics",created==0 && monitor.WindowsSampleCount==0);
        settings.ShowMonitor=true; monitor.Configure(settings,true);
        check("Enable wakes both sampling lanes",SpinWait.SpinUntil(()=>Volatile.Read(ref fake.Reads)>0 && monitor.WindowsSampleCount>0,4000));
        monitor.Configure(settings,false);
        check("Hidden monitor closes native provider",SpinWait.SpinUntil(()=>Volatile.Read(ref fake.Disposals)>0,3000));
        Thread.Sleep(150);
        long hiddenCount=monitor.WindowsSampleCount;int hiddenReads=Volatile.Read(ref fake.Reads);
        Thread.Sleep(1150);
        check("Hidden monitor sleeps without polling",monitor.WindowsSampleCount==hiddenCount && fake.Reads==hiddenReads);
        monitor.Configure(settings,true);
        check("Reveal resumes both lanes with fresh provider",SpinWait.SpinUntil(()=>Volatile.Read(ref created)==2 && fake.Reads>hiddenReads && monitor.WindowsSampleCount>hiddenCount,4000));
        settings.ShowMonitor=false; monitor.Configure(settings,false);
        check("Disable promptly releases provider",SpinWait.SpinUntil(()=>Volatile.Read(ref fake.Disposals)>0,3000));
        Thread.Sleep(150);
        long count=monitor.WindowsSampleCount; int reads=Volatile.Read(ref fake.Reads);
        Thread.Sleep(1150);
        check("Disabled lanes stay asleep past former sampling interval",monitor.WindowsSampleCount==count && fake.Reads==reads);
        monitor.Dispose();
        check("Shutdown completes while both lanes sleep",monitor.Completion.Wait(3000));
        monitor.Configure(settings,false); monitor.Start();
        check("Late configure and start remain harmless after disposal",true);
    }
    private static void NetworkLifecycle(Action<string,bool> check)
    {
        var metrics=new SystemMetrics();
        var off=metrics.ReadNetwork("",enabled:false);
        check("Disabled network skips adapter APIs and exposes no stale rate",metrics.NetworkSampleCount==0 && off.Upload==null && off.Download==null && off.Name=="");
        using var monitor=new HardwareMonitorService(()=>new FakeProvider(),1000);
        var settings=new AppSettings {ShowMonitor=true,ShowNetwork=false,HardwareSensorsEnabled=false,EcoMode=false,MonitorUpdateIntervalMs=1000};
        monitor.Configure(settings,true);monitor.Start();
        check("CPU/RAM sampling continues with network card off",SpinWait.SpinUntil(()=>monitor.GetSnapshot().Sequence>0,3000) && monitor.NetworkSampleCount==0);
        long sequence=monitor.GetSnapshot().Sequence;
        settings.ShowNetwork=true;monitor.Configure(settings,true);
        check("Network resumes with a fresh delta baseline",SpinWait.SpinUntil(()=>monitor.GetSnapshot().Sequence>sequence,3000)
            && monitor.NetworkSampleCount>0 && monitor.GetSnapshot().NetDownload==null && monitor.GetSnapshot().NetUpload==null);
        sequence=monitor.GetSnapshot().Sequence;
        settings.ShowNetwork=false;monitor.Configure(settings,true);
        check("Disabling network clears published rates",SpinWait.SpinUntil(()=>monitor.GetSnapshot().Sequence>sequence,3000)
            && monitor.GetSnapshot().NetDownload==null && monitor.GetSnapshot().NetworkName=="");
        long count=monitor.NetworkSampleCount;
        Thread.Sleep(1150);
        check("Network stays asleep while CPU/RAM still update",monitor.NetworkSampleCount==count && monitor.GetSnapshot().Sequence>sequence+1);
        monitor.Dispose();check("Network lifecycle fixture shuts down",monitor.Completion.Wait(3000));
    }
}
