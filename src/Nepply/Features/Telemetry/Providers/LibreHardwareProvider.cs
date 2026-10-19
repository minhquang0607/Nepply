using System.Collections.Immutable;
using System.Diagnostics;
using Nepply.Models;
using LibreHardwareMonitor.Hardware;
using static Nepply.Models.MetricValue;
namespace Nepply.Services.Telemetry;

// This provider exclusively owns Open/Update/Close and the ADL context.
public sealed class LibreHardwareProvider : IHardwareProvider
{
    private Computer? _computer;
    private readonly AmdTelemetry _amd = new();
    private (string Id, string Name)[] _gpuInventory = [];
    private Dictionary<string, GpuKind> _gpuKinds = [];
    public string Name => "LibreHardwareMonitor + AMD ADL";
    public HardwareReading Read(AppSettings settings)
    {
    if (_computer == null)
    {
        _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true, IsMotherboardEnabled = false, IsMemoryEnabled = true };
        _computer.Open();
    }
    var available = new List<IHardware>();
    foreach (var hardware in _computer.Hardware)
        UpdateTree(hardware, available);
    var cpu = available.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
    var amdSamples = available.Where(h => h.HardwareType == HardwareType.GpuAmd).ToDictionary(h => h.Identifier.ToString(), h => _amd.Read(h.Identifier.ToString()));
    var cpuCandidates = amdSamples.Values.Where(s => s?.CpuTemperature != null).ToArray();
    var cpuFallback = available.Count(h => h.HardwareType == HardwareType.Cpu) == 1 && cpu?.Name.Contains("AMD", StringComparison.OrdinalIgnoreCase) == true && cpuCandidates.Length == 1 ? cpuCandidates[0] : null;
    var cpuTemperatureSensor = SelectSensor(cpu, SensorType.Temperature, 1, 130, SensorSelection.CpuTemperatureNames);
    var cpuTemperature = cpuTemperatureSensor?.Value;
    var cpuClock = CpuClock(cpu);
    var gpuHardware = available.Where(IsGpu).ToArray();
    if (!_gpuInventory.SequenceEqual(gpuHardware.Select(h => (h.Identifier.ToString(), h.Name))))
    {
        _gpuInventory = gpuHardware.Select(h => (h.Identifier.ToString(), h.Name)).ToArray();
        var adapters = GpuAdapterCatalog.Read();
        _gpuKinds = _gpuInventory.ToDictionary(g => g.Id, g => GpuAdapterCatalog.Classify(g.Name, adapters));
    }
    var gpus = gpuHardware.Select(h => new GpuReading(h.Identifier.ToString(), h.Name,
        GpuLoad(h), Sensor(h, SensorType.Temperature, 1, 130, SensorSelection.GpuCoreNames),
        Sensor(h, SensorType.Clock, 1, 15000, "GPU Core", "Core"), "LibreHardwareMonitor · " + h.HardwareType + " · " + (HasGpuEngines(h) ? "load: busiest D3D engine" : "load: GPU Core"))
        { Kind = _gpuKinds.GetValueOrDefault(h.Identifier.ToString()), SourceInfo = new(h.HardwareType.ToString(),HasGpuEngines(h),false) })
        .Select(g => g.Temperature == null && amdSamples.GetValueOrDefault(g.Id)?.GpuTemperature is { } temperature ? g with { Temperature = temperature, Source = g.Source + " · temperature: AMD ADL PMLog EDGE", SourceInfo = g.SourceInfo! with {AdlTemperature=true} } : g)
        .DistinctBy(g => g.Id).ToImmutableArray();
    var gpu = available.FirstOrDefault(h => h.Identifier.ToString() == settings.GpuIdentifier && IsGpu(h))
        ?? available.FirstOrDefault(IsGpu);
    var memoryClock = available.Where(h => h.HardwareType == HardwareType.Memory).SelectMany(h => h.Sensors)
        .FirstOrDefault(s => s.SensorType == SensorType.Clock);
    var reading = new HardwareReading(DateTimeOffset.UtcNow, cpu?.Name ?? "CPU", gpu?.Name ?? "GPU",
        cpuTemperature ?? cpuFallback?.CpuTemperature,
        cpuClock ?? cpuFallback?.CpuClockMHz,
        gpu == null ? null : GpuLoad(gpu),
        gpus.FirstOrDefault(g => g.Id == gpu?.Identifier.ToString())?.Temperature,
        Sensor(gpu, SensorType.Clock, 1, 15000, "GPU Core", "Core"),
        Valid(memoryClock?.Value, 1, 15000), null)
    {
        Gpus = gpus, MonotonicTimestamp = Stopwatch.GetTimestamp(),
        CpuSourceInfo = new(
            cpuTemperatureSensor != null ? "LibreHardwareMonitor · " + cpuTemperatureSensor.Name
                : cpuFallback?.CpuTemperature != null ? "AMD ADL PMLog · CPU temperature" : null,
            cpuClock != null ? "LibreHardwareMonitor · reported clock"
                : cpuFallback?.CpuClockMHz != null ? "AMD ADL PMLog · reported clock" : null,
            cpuTemperatureSensor == null && cpuFallback?.CpuTemperature != null,
            cpuClock == null && cpuFallback?.CpuClockMHz != null),
        CpuTemperatureSourceId = cpuTemperatureSensor != null ? "lhm:" + cpuTemperatureSensor.Id
            : cpuFallback?.CpuTemperature != null ? "adl:cpu:" + cpu?.Identifier : "",
        CpuTelemetrySource = "Temperature: " + (cpuTemperatureSensor != null
            ? "LibreHardwareMonitor · " + cpuTemperatureSensor.Name
            : cpuFallback?.CpuTemperature != null ? "AMD ADL PMLog · CPU temperature (fallback)" : "unavailable")
            + " · Clock: " + (cpuClock != null ? "LibreHardwareMonitor · reported clock"
                : cpuFallback?.CpuClockMHz != null ? "AMD ADL PMLog · reported clock (fallback)" : "unavailable")
    };

        return reading;
    }
    private static bool IsGpu(IHardware hardware) => hardware.HardwareType is HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia;
    private static float? CpuClock(IHardware? cpu)
    {
        var average = cpu?.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Clock && s.Name.Contains("Average", StringComparison.OrdinalIgnoreCase)
            && !s.Name.Contains("Effective", StringComparison.OrdinalIgnoreCase) && Valid(s.Value, 1, 15000) != null)?.Value;
        if (average != null) return average;
        var cores = cpu?.Sensors.Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) && !s.Name.Contains("Effective", StringComparison.OrdinalIgnoreCase))
            .Select(s => Valid(s.Value, 1, 15000)).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        return cores is { Length: > 0 } ? cores.Average() : null;
    }
    private static float? GpuLoad(IHardware gpu)
    {
        var engines = gpu.Sensors.Where(s => s.SensorType == SensorType.Load && s.Name.StartsWith("D3D ", StringComparison.OrdinalIgnoreCase))
            .Select(s => Valid(s.Value, 0, 100)).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        return engines is { Length: > 0 } ? engines.Max() : Sensor(gpu, SensorType.Load, 0, 100, "GPU Core");
    }
    private static bool HasGpuEngines(IHardware gpu) => gpu.Sensors.Any(s => s.SensorType == SensorType.Load && s.Name.StartsWith("D3D ", StringComparison.OrdinalIgnoreCase) && Valid(s.Value, 0, 100) != null);
    private static void UpdateTree(IHardware hardware, List<IHardware> available)
    {
        try
        {
            // Nepply presents latest values only; do not retain LHM's graph history.
            DisableHistory(hardware);
            hardware.Update();
            DisableHistory(hardware); // Some sensors are activated during Update.
            available.Add(hardware);
        }
        catch (Exception ex) { AppLogger.Error(ex, "Read " + hardware.Name); }
        foreach (var child in hardware.SubHardware) UpdateTree(child, available);
    }
    private static void DisableHistory(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
            if (sensor.ValuesTimeWindow != TimeSpan.Zero) sensor.ValuesTimeWindow = TimeSpan.Zero;
    }
    private static float? Sensor(IHardware? hardware, SensorType type, float minimum, float maximum, params string[] names)
        => SelectSensor(hardware, type, minimum, maximum, names)?.Value;

    private static NamedSensorValue? SelectSensor(IHardware? hardware, SensorType type,
        float minimum, float maximum, params string[] names)
        => hardware == null ? null : SensorSelection.Select(hardware.Sensors
            .Where(s => s.SensorType == type)
            .Select(s => new NamedSensorValue(s.Identifier.ToString(), s.Name, s.Value)), minimum, maximum, names);

    public void Dispose()
    {
        try { _amd.Dispose(); }
        finally { try { _computer?.Close(); } finally { _computer = null; } }
    }
}
