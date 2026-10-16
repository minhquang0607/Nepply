using System.Diagnostics;
using Nepply.Models;
using Nepply.Services.Telemetry;

internal static class SensorDiagnosticReportTests
{
    internal static void Run(Action<string,bool> check)
    {
        long now=Stopwatch.GetTimestamp();
        var snapshot=HardwareInfoSnapshot.Empty with {Sequence=1,MonotonicTimestamp=now,
            NetworkName="PRIVATE_NETWORK",Drives=[new("Z:","PRIVATE_VOLUME",1,2)],
            Hardware=HardwareReading.Empty with {CpuName="Fixture CPU",CpuTemp=45,
                Error="PRIVATE_RAW_ERROR",
                Gpus=[new("PRIVATE_GPU_ID","Fixture GPU",20,42,1000,"fixture GPU source")]}};
        string report=SensorDiagnosticReport.Build(snapshot,"test privilege","test registration",now);
        check("Diagnostic report excludes deferred mainboard and fans",!report.Contains("MAINBOARD") && !report.Contains("RPM")); 
        check("Diagnostic report includes CPU GPU and source",report.Contains("Fixture CPU") && report.Contains("Fixture GPU") && report.Contains("fixture GPU source"));
        check("Diagnostic allowlist excludes IDs network volumes and raw errors",!report.Contains("PRIVATE_"));
        check("Diagnostic report marks capture rather than live data",report.Contains("không cập nhật trực tiếp"));
        check("Diagnostic report labels old sample",SensorDiagnosticReport.Build(snapshot,"","",now+Stopwatch.Frequency*21).Contains("Mẫu đã cũ"));
        check("Diagnostic report handles missing sample",SensorDiagnosticReport.Build(null,"","",now).Contains("Chưa nhận được mẫu"));
        var agedHardware = snapshot with { Hardware = snapshot.Hardware with { MonotonicTimestamp = now - Stopwatch.Frequency * 30 } };
        string agedReport = SensorDiagnosticReport.Build(agedHardware,"","",now);
        check("Diagnostic separates sensor age from fresh aggregate",agedReport.Contains("Tuổi mẫu tổng hợp: 0") && agedReport.Contains("Tuổi lượt đọc cảm biến phần cứng: 30"));
        check("Diagnostic does not infer physical sensor freshness from polling",agedReport.Contains("không xác nhận driver"));
        check("Diagnostic missing sensor clock is explicitly unknown",report.Contains("Tuổi lượt đọc cảm biến phần cứng: chưa xác định"));
        check("Diagnostic future sensor clock is not a negative age",SensorDiagnosticReport.Build(snapshot with {Hardware=snapshot.Hardware with {MonotonicTimestamp=now+Stopwatch.Frequency}},"","",now).Contains("Tuổi lượt đọc cảm biến phần cứng: chưa xác định"));
    }
}
