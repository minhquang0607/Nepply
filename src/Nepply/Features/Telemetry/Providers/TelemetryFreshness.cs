using System.Collections.Immutable;
using System.Diagnostics;
using Nepply.Models;

namespace Nepply.Services.Telemetry;

internal static class TelemetryFreshness
{
    internal static bool IsSnapshotStale(HardwareInfoSnapshot snapshot,long now)
    {
        if(snapshot.Sequence==0) return false;
        if(snapshot.MonotonicTimestamp<=0 || now<snapshot.MonotonicTimestamp) return true;
        return Stopwatch.GetElapsedTime(snapshot.MonotonicTimestamp,now)>TimeSpan.FromSeconds(20);
    }
    public static HardwareReading ExpireIfNeeded(HardwareReading reading, int intervalMs)
    {
        if (reading.MonotonicTimestamp == 0) return reading;
        return IsExpired(Stopwatch.GetElapsedTime(reading.MonotonicTimestamp),intervalMs)
            ? Unavailable(reading,"SensorExpired") : reading;
    }
    internal static bool IsExpired(TimeSpan elapsed,int intervalMs)
        => elapsed < TimeSpan.Zero || elapsed > TimeSpan.FromMilliseconds(Math.Max(10000L,(long)intervalMs*3));

    /// <param name="reasonKey">Resource key of the status shown while measurements are unavailable.</param>
    public static HardwareReading Unavailable(HardwareReading previous,string reasonKey)
    {
        // Identity is retained only as last-known inventory; every measurement and
        // its source description is cleared.
        var gpus=previous.Gpus.Select(g=>g with { Load=null,Temperature=null,ClockMHz=null,Source="",SourceInfo=null }).ToImmutableArray();
        return previous with
        {
            CpuTemp=null,CpuClockMHz=null,GpuLoad=null,GpuTemp=null,GpuClockMHz=null,
            MemoryClockMHz=null,Gpus=gpus,VisibleGpus=[],
            Error=reasonKey,CpuTelemetrySource="",CpuSourceInfo=null
        };
    }
}
