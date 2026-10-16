using Nepply.Models;
using Nepply.Services;
using Nepply.Services.Telemetry;

internal static class TelemetryFreshnessTests
{
    public static void Run(Action<string,bool> check)
    {
        check("Sub GHz clock uses readable MHz",TelemetryDisplay.Clock(400)=="400 MHz");
        check("GHz boundary uses GHz",TelemetryDisplay.Clock(1000).EndsWith(" GHz"));
        check("Invalid clocks never masquerade as zero",new float?[]{null,0,-1,float.NaN,float.PositiveInfinity}.All(v=>TelemetryDisplay.Clock(v)=="—"));
        check("GPU and CPU share clock formatting",new GpuReading("gpu","GPU",0,null,400,"fixture").ClockText==TelemetryDisplay.Clock(400));
        check("Deferred telemetry is absent from schema",typeof(HardwareReading).GetProperties().All(p=>!p.Name.Contains("Board") && !p.Name.Contains("Fan")) && typeof(AppSettings).GetProperty("BoardSensorIdentifier")==null);
        var legacy=System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"BoardSensorIdentifier\":\"old-board\",\"ShowMonitor\":true}");
        check("Legacy board setting is safely ignored",legacy?.ShowMonitor==true);
        long now=System.Diagnostics.Stopwatch.Frequency*100;
        var timed=HardwareInfoSnapshot.Empty with { Sequence=1,SampledAt=DateTimeOffset.UtcNow.AddYears(-1),MonotonicTimestamp=now };
        check("Wall clock moving backward does not mark fresh data stale",!TelemetryFreshness.IsSnapshotStale(timed,now));
        check("Wall clock moving forward cannot hide stale data",TelemetryFreshness.IsSnapshotStale(timed with {SampledAt=DateTimeOffset.UtcNow.AddYears(1)},now+System.Diagnostics.Stopwatch.Frequency*21));
        check("Snapshot freshness boundary uses monotonic duration",!TelemetryFreshness.IsSnapshotStale(timed,now+System.Diagnostics.Stopwatch.Frequency*20));
        check("Missing snapshot clock is not trusted",TelemetryFreshness.IsSnapshotStale(timed with {MonotonicTimestamp=0},now));
        check("Initial empty snapshot is connecting not stale",!TelemetryFreshness.IsSnapshotStale(HardwareInfoSnapshot.Empty,now));
        check("Native sample stays fresh at deadline",!TelemetryFreshness.IsExpired(TimeSpan.FromSeconds(10),1500));
        check("Native sample expires beyond deadline",TelemetryFreshness.IsExpired(TimeSpan.FromMilliseconds(10001),1500));
        check("Eco cadence adjusts native freshness deadline",!TelemetryFreshness.IsExpired(TimeSpan.FromSeconds(14),5000) && TelemetryFreshness.IsExpired(TimeSpan.FromSeconds(16),5000));
        check("Invalid monotonic age does not look fresh",TelemetryFreshness.IsExpired(TimeSpan.FromSeconds(-1),1500));
        var original=new HardwareReading(DateTimeOffset.UtcNow,"CPU fixture","GPU fixture",50,3500,40,60,1500,1600,null)
        { Gpus=[new("gpu-1","GPU fixture",40,60,1500,"fixture")],MonotonicTimestamp=System.Diagnostics.Stopwatch.GetTimestamp() };
        var expired=TelemetryFreshness.Unavailable(original,"Test expired");
        check("Expired native snapshot clears every measurement",expired.CpuTemp==null && expired.CpuClockMHz==null && expired.GpuLoad==null && expired.GpuTemp==null && expired.GpuClockMHz==null && expired.MemoryClockMHz==null);
        check("Expired GPU keeps identity without stale numbers",expired.Gpus.Length==1 && expired.Gpus[0].Id=="gpu-1" && expired.Gpus[0].Load==null && expired.Gpus[0].Temperature==null && expired.Gpus[0].ClockMHz==null && expired.Gpus[0].Source=="Test expired");
        check("Expired explicit GPU selection stays visible as unavailable",HardwareMonitorService.SelectGpus(expired.Gpus,new AppSettings { GpuDisplay=GpuDisplayMode.Primary,GpuIdentifier="gpu-1" }).Length==1);
        check("Expiring snapshot does not mutate original",original.CpuTemp==50 && original.Gpus[0].Temperature==60);
        check("Fresh snapshot is reused without allocation",ReferenceEquals(TelemetryFreshness.ExpireIfNeeded(original,1500),original));
    }
    public static void RunRecovery(Action<string,bool> check)
    {
        int attempts=0;
        using var failed=new ManualResetEventSlim();
        using var recovered=new ManualResetEventSlim();
        using var service=new HardwareMonitorService(()=>new IntermittentProvider(()=>Interlocked.Increment(ref attempts)),1000);
        service.Configure(new AppSettings { EcoMode=false,MonitorUpdateIntervalMs=1000 },true);
        service.HardwareUpdated+=snapshot=>
        {
            var gpu=snapshot.Hardware.Gpus.FirstOrDefault();
            if(snapshot.Hardware.Error!=null && gpu?.Id=="test-gpu" && gpu.Temperature==null) failed.Set();
            if(failed.IsSet && snapshot.Hardware.Error==null && gpu?.Temperature==63) recovered.Set();
        };
        service.Start();
        check("Provider failure keeps last GPU identity with missing measurements",failed.Wait(6000));
        check("Provider reconnect restores fresh GPU measurements",recovered.Wait(15000));
        RunToggleRecovery(check);
        RunPolicyFailure(check);
    }
    private static void RunPolicyFailure(Action<string,bool> check)
    {
        int attempts=0;
        using var notified=new ManualResetEventSlim();
        using var service=new HardwareMonitorService(()=>
        {
            Interlocked.Increment(ref attempts);
            throw new System.ComponentModel.Win32Exception(4551,"Injected policy denial; no process launched");
        });
        service.Configure(new AppSettings {EcoMode=false,MonitorUpdateIntervalMs=1000},true);
        service.HardwareUpdated+=frame=>
        {
            if(frame.Hardware.Error==HelperFailure.PolicyMessage) notified.Set();
        };
        service.Start();
        check("Policy-blocked sensor source reports actionable status",notified.Wait(6000));
        long sequence=service.GetSnapshot().Sequence;
        check("Windows metrics continue across policy retry deadline",SpinWait.SpinUntil(()=>service.GetSnapshot().Sequence>=sequence+12,16000));
        check("Policy denial suppresses automatic helper relaunch",Volatile.Read(ref attempts)==1);
        check("Policy status is not overwritten by stale sensor warning",service.GetSnapshot().Hardware.Error==HelperFailure.PolicyMessage);
    }
    private static void RunToggleRecovery(Action<string,bool> check)
    {
        int created=0, closed=0;
        using var initial=new ManualResetEventSlim();
        using var recovered=new ManualResetEventSlim();
        using var disabled=new ManualResetEventSlim();
        using var service=new HardwareMonitorService(()=>new ThrowingCloseProvider(
            Interlocked.Increment(ref created),()=>Interlocked.Increment(ref closed)),1000);
        var settings=new AppSettings { EcoMode=false,MonitorUpdateIntervalMs=1000 };
        service.Configure(settings,true);
        service.HardwareUpdated+=snapshot=>
        {
            if(snapshot.Hardware.CpuTemp==41) initial.Set();
            if(snapshot.Hardware.CpuTemp==42) recovered.Set();
            if(snapshot.Hardware.Error=="Cảm biến nâng cao đã tắt") disabled.Set();
        };
        service.Start();
        check("Toggle fixture produces initial sensor sample",initial.Wait(6000));
        settings.HardwareSensorsEnabled=false;
        service.Configure(settings,true);
        check("Windows metrics continue while sensors are disabled",disabled.Wait(6000));
        check("Disabled provider cleanup was attempted",SpinWait.SpinUntil(()=>Volatile.Read(ref closed)>0,6000));
        settings.HardwareSensorsEnabled=true;
        service.Configure(settings,true);
        bool resumed=recovered.Wait(6000);
        if (!resumed)
        {
            var worker=(Task?)typeof(HardwareMonitorService).GetField("_sensorTask",System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(service);
            check($"Toggle diagnostics: created={Volatile.Read(ref created)}, closed={Volatile.Read(ref closed)}, worker={worker?.Status}, error={worker?.Exception}, temperature={service.GetSnapshot().Hardware.CpuTemp}",false);
        }
        check("Sensors restart after provider throws during disable",resumed);
        check("Restart creates a fresh provider without reusing failed instance",Volatile.Read(ref created)==2 && Volatile.Read(ref closed)==1);
    }
    private sealed class ThrowingCloseProvider(int generation,Action closed) : IHardwareProvider
    {
        public string Name=>"Throwing cleanup fixture";
        public HardwareReading Read(AppSettings settings)=>HardwareReading.Empty with
        {
            MonotonicTimestamp=System.Diagnostics.Stopwatch.GetTimestamp(),
            SampledAt=DateTimeOffset.UtcNow,CpuTemp=40+generation
        };
        public void Dispose()
        {
            closed();
            throw new System.IO.IOException("Injected cleanup failure");
        }
    }
    private sealed class IntermittentProvider(Func<int> attempt) : IHardwareProvider
    {
        public string Name=>"Test fixture";
        public HardwareReading Read(AppSettings settings)
        {
            int current=attempt();
            if(current==2) throw new System.IO.IOException("Injected driver failure");
            return HardwareReading.Empty with { MonotonicTimestamp=System.Diagnostics.Stopwatch.GetTimestamp(),SampledAt=DateTimeOffset.UtcNow,Gpus=[new("test-gpu","Test GPU",30,current==1 ? 50 : 63,1500,"fixture")] };
        }
        public void Dispose() { }
    }
}
