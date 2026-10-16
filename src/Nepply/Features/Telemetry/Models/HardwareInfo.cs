using System.Collections.Immutable;
namespace Nepply.Models;

public enum GpuKind { Unknown, Integrated, Discrete }

public sealed record CpuSourceInfo(string? Temperature,string? Clock,bool TemperatureFallback,bool ClockFallback);
public sealed record GpuSourceInfo(string HardwareType,bool D3dEngines,bool AdlTemperature);

public sealed record GpuReading(string Id, string Name, float? Load, float? Temperature, float? ClockMHz, string Source)
{
    public GpuKind Kind { get; init; }
    public GpuSourceInfo? SourceInfo { get; init; }
    public double LoadValue => Load ?? 0;
    public string LoadText => Load is { } value ? $"{value:F0}%" : "—";
    public string TemperatureText => Temperature is { } value ? $"{value:F0}°C" : "";
    public string ClockText => TelemetryDisplay.Clock(ClockMHz);
    public string Label { get; init; } = "GPU";
    public string Details => Name + "\n" + Source + "\n" + (Temperature == null ? "Nhiệt độ chưa khả dụng" : "Nhiệt độ GPU core/edge");
}
public sealed record DiskDriveInfo(string Letter, string Name, double UsedGB, double TotalGB)
{
    public double UsagePercent => TotalGB > 0 ? UsedGB / TotalGB * 100 : 0;
    public string CapacityText => $"{UsedGB:F1} / {TotalGB:F0} GiB";
}
public sealed record HardwareReading(
    DateTimeOffset SampledAt, string CpuName, string GpuName,
    float? CpuTemp, float? CpuClockMHz, float? GpuLoad, float? GpuTemp, float? GpuClockMHz,
    float? MemoryClockMHz, string? Error)
{
    public ImmutableArray<GpuReading> Gpus { get; init; } = [];
    public long MonotonicTimestamp { get; init; }
    public string CpuTelemetrySource { get; init; } = "LibreHardwareMonitor";
    public CpuSourceInfo? CpuSourceInfo { get; init; }
    public string CpuTemperatureSourceId { get; init; } = "";
    public ImmutableArray<GpuReading> VisibleGpus { get; init; } = [];
    public static HardwareReading Empty { get; } = new(default, "CPU", "GPU", null, null, null, null, null, null, null);
}
public sealed record HardwareInfoSnapshot(
    long Sequence, DateTimeOffset SampledAt, float? CpuLoad,
    double? RamUsedGB, double? RamTotalGB, float? RamLoad,
    double? NetUpload, double? NetDownload, string NetworkName,
    HardwareReading Hardware, ImmutableArray<DiskDriveInfo> Drives)
{
    public double? RamInstalledGB { get; init; }
    public long MonotonicTimestamp { get; init; } = System.Diagnostics.Stopwatch.GetTimestamp();
    public static HardwareInfoSnapshot Empty { get; } = new(0, default, null, null, null, null, null, null, "Chưa có kết nối", HardwareReading.Empty, []) { MonotonicTimestamp=0 };
}
