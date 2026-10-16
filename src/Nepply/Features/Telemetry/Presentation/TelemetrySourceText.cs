using Nepply.Models;
using Nepply.Services;

namespace Nepply.ViewModels;

internal static class TelemetrySourceText
{
    private static string Format(string key,params object?[] args)=>string.Format(LocalizationService.Get(key),args);
    private static string Sensor(string? source,bool fallback)=>source==null ? LocalizationService.Get("ReportUnavailable")
        : fallback ? Format("SourceFallback",source) : source;
    internal static string Cpu(HardwareReading reading)=>reading.CpuSourceInfo is {} source
        ? Format("SourceCpu",Sensor(source.Temperature,source.TemperatureFallback),Sensor(source.Clock,source.ClockFallback))
        : LocalizationService.Translate(reading.CpuTelemetrySource);
    internal static string Gpu(GpuReading reading)=>reading.SourceInfo is {} source
        ? Format("SourceGpu","LibreHardwareMonitor · "+source.HardwareType,
            source.D3dEngines ? LocalizationService.Get("SourceD3dLoad") : "GPU Core")
            + (source.AdlTemperature ? " · "+Format("ReportTemperature","AMD ADL PMLog EDGE") : "")
        : LocalizationService.Translate(reading.Source);
    internal static string GpuDetails(GpuReading reading)=>reading.Name+"\n"+Gpu(reading)+"\n"
        +LocalizationService.Get(reading.Temperature==null ? "TemperatureUnavailable" : "GpuCoreTemperature");
}
