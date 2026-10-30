using System.Diagnostics;
using System.Reflection;
using System.Text;
using Nepply.Models;

namespace Nepply.Services.Telemetry;

internal static class SensorDiagnosticReport
{
    /// <param name="privilegeKey">Resource key of the process privilege state.</param>
    /// <param name="driverKey">Resource key of the PawnIO registration state.</param>
    internal static string Build(HardwareInfoSnapshot? snapshot, string privilegeKey, string driverKey, long now)
    {
        string Format(string key, params object?[] arguments) => string.Format(LocalizationService.Get(key), arguments);
        string Temperature(float? value) => value is { } number ? $"{number:F1}°C" : LocalizationService.Get("ReportUnavailable");
        var report = new StringBuilder();
        report.AppendLine(LocalizationService.Get("ReportTitle"));
        report.AppendLine(Format("ReportVersion", typeof(SensorDiagnosticReport).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion));
        report.AppendLine("Windows: " + Environment.OSVersion.Version + (Environment.Is64BitOperatingSystem ? " x64" : ""));
        report.AppendLine(Format("ReportPrivilege", LocalizationService.Get(privilegeKey)));
        report.AppendLine("PawnIO: " + LocalizationService.Get(driverKey));
        report.AppendLine(LocalizationService.Get("ReportCapture"));
        if (snapshot == null || snapshot.Sequence == 0) { report.AppendLine(LocalizationService.Get("ReportNoSample")); return report.ToString(); }
        double age = snapshot.MonotonicTimestamp > 0 && now >= snapshot.MonotonicTimestamp
            ? Stopwatch.GetElapsedTime(snapshot.MonotonicTimestamp, now).TotalSeconds : double.NaN;
        report.AppendLine(double.IsFinite(age) ? Format("ReportAggregateAge", age) : LocalizationService.Get("ReportAggregateUnknown"));
        bool stale = TelemetryFreshness.IsSnapshotStale(snapshot, now);
        report.AppendLine(LocalizationService.Get(stale ? "ReportStale" : "ReportRecent"));
        var h = snapshot.Hardware;
        double sensorAge = h.MonotonicTimestamp > 0 && now >= h.MonotonicTimestamp
            ? Stopwatch.GetElapsedTime(h.MonotonicTimestamp, now).TotalSeconds : double.NaN;
        report.AppendLine(double.IsFinite(sensorAge)
            ? Format("ReportSensorAge", sensorAge)
            : LocalizationService.Get("ReportSensorUnknown"));
        report.AppendLine(LocalizationService.Get("ReportFreshnessHint"));
        report.AppendLine(); report.AppendLine("CPU: " + h.CpuName);
        report.AppendLine(Format("ReportTemperature", Temperature(h.CpuTemp)));
        report.AppendLine(Format("ReportClock", TelemetryDisplay.Clock(h.CpuClockMHz)));
        report.AppendLine(Format("ReportSource", ViewModels.TelemetrySourceText.Cpu(h)));
        foreach (var gpu in h.Gpus)
        {
            report.AppendLine(); report.AppendLine("GPU: " + gpu.Name);
            report.AppendLine(Format("ReportSource", ViewModels.TelemetrySourceText.Gpu(gpu)));
            report.AppendLine(Format("ReportGpuValues", gpu.LoadText, Temperature(gpu.Temperature)));
            report.AppendLine(Format("ReportClock", gpu.ClockText));
        }
        report.AppendLine(); report.AppendLine(LocalizationService.Get("ReportPrivacy"));
        return report.ToString();
    }
}
