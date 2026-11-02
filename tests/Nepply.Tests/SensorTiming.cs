using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Nepply.Models;
using Nepply.Services.Telemetry;

// Measures the helper a panel reveal waits for. Needs a published Nepply.exe and real sensors.
internal static class SensorTiming
{
    internal static int Run(string output, string executable)
    {
        Directory.CreateDirectory(output);
        var settings = new AppSettings();
        var cold = new List<double>();
        var warm = new List<double>();
        double workingSetMiB = 0, privateMiB = 0, idleCpuSeconds = 0;
        for (int round = 0; round < 3; round++)
        {
            using var provider = new SensorProcessProvider(executable);
            var clock = Stopwatch.StartNew();
            provider.Read(settings);
            cold.Add(clock.Elapsed.TotalMilliseconds);
            for (int i = 0; i < 3; i++)
            {
                Thread.Sleep(1500);
                clock.Restart();
                provider.Read(settings);
                warm.Add(clock.Elapsed.TotalMilliseconds);
            }
            if (provider.DiagnosticProcess is not { } host) continue;
            var before = host.TotalProcessorTime;
            Thread.Sleep(10000);
            host.Refresh();
            workingSetMiB = host.WorkingSet64 / 1048576d;
            privateMiB = host.PrivateMemorySize64 / 1048576d;
            idleCpuSeconds = (host.TotalProcessorTime - before).TotalSeconds;
        }
        var revealMs = new List<double>();
        using (var monitor = new Nepply.Services.HardwareMonitorService(() => new SensorProcessProvider(executable)))
        {
            var visible = new AppSettings { ShowMonitor = true, HardwareSensorsEnabled = true };
            monitor.Configure(visible, true); monitor.Start();
            SpinWait.SpinUntil(() => monitor.GetSnapshot().Hardware.MonotonicTimestamp > 0, 20000);
            for (int i = 0; i < 5; i++)
            {
                monitor.Configure(visible, false);
                Thread.Sleep(2000);
                long sequence = monitor.GetSnapshot().Sequence;
                var clock = Stopwatch.StartNew();
                monitor.Configure(visible, true);
                SpinWait.SpinUntil(() => monitor.GetSnapshot().Sequence > sequence, 5000);
                var sample = monitor.GetSnapshot();
                revealMs.Add(sample.CpuLoad != null ? clock.Elapsed.TotalMilliseconds : -1);
            }
        }
        var result = new { ColdReadMs = cold, WarmReadMs = warm, WorkingSetMiB = workingSetMiB, PrivateMiB = privateMiB, IdleCpuSecondsPer10s = idleCpuSeconds, RevealToNumbersMs = revealMs };
        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(output, "sensor-timing.json"), json);
        Console.WriteLine(json);
        return 0;
    }
}
