using Nepply.Models;
using Nepply.Services;

namespace Nepply.ViewModels;

internal static class TelemetrySourceText
{
    private static string Format(string key, params object?[] args) => string.Format(LocalizationService.Get(key), args);
    private static string Sensor(string? source, bool fallback) => source == null ? LocalizationService.Get("ReportUnavailable")
        : fallback ? Format("SourceFallback", source) : source;
    // Without structured metadata the reading was cleared or never sampled: show its
    // status, or the language-neutral diagnostic text a provider supplied. Anything
    // that is not a known status key is never echoed (diagnostic reports are shared).
    private static string Legacy(string? statusKey, string diagnostic) => statusKey != null && LocalizationService.TryGet(statusKey, out var status) ? status
        : diagnostic.Length > 0 ? diagnostic : LocalizationService.Get("ReportUnavailable");
    internal static string Cpu(HardwareReading reading) => reading.CpuSourceInfo is { } source
        ? Format("SourceCpu", Sensor(source.Temperature, source.TemperatureFallback), Sensor(source.Clock, source.ClockFallback))
        : Legacy(reading.Error, reading.CpuTelemetrySource);
    internal static string Gpu(GpuReading reading) => reading.SourceInfo is { } source
        ? Format("SourceGpu", "LibreHardwareMonitor · " + source.HardwareType,
            source.D3dEngines ? LocalizationService.Get("SourceD3dLoad") : "GPU Core")
            + (source.AdlTemperature ? " · " + Format("ReportTemperature", "AMD ADL PMLog EDGE") : "")
        : Legacy(null, reading.Source);
    internal static string GpuDetails(GpuReading reading) => reading.Name + "\n" + Gpu(reading) + "\n"
        + LocalizationService.Get(reading.Temperature == null ? "TemperatureUnavailable" : "GpuCoreTemperature");
}
