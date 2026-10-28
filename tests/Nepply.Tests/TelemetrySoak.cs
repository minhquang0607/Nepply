using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Nepply.Models;
using Nepply.Services;
using Nepply.Services.Telemetry;

internal static class TelemetrySoak
{
    internal sealed record Acceptance(bool ActiveSensorRead, bool ResumedSensorRead,
        bool HiddenSamplingStopped, bool HelpersStopped, bool LoopsStopped)
    {
        public bool Passed => ActiveSensorRead && ResumedSensorRead && HiddenSamplingStopped && HelpersStopped && LoopsStopped;
    }

    internal static bool HasNativeMeasurement(HardwareReading reading) => reading.Error == null
        && reading.MonotonicTimestamp > 0
        && (reading.CpuTemp is { } temperature && float.IsFinite(temperature)
            || reading.CpuClockMHz is { } clock && float.IsFinite(clock)
            || reading.Gpus.Any(g => g.Load is { } load && float.IsFinite(load)
                || g.Temperature is { } temp && float.IsFinite(temp)
                || g.ClockMHz is { } speed && float.IsFinite(speed)));

    internal static void VerifyAcceptance(Action<string, bool> check)
    {
        var good = new Acceptance(true, true, true, true, true);
        check("Soak requires all lifecycle and sensor gates", good.Passed);
        check("Soak rejects missing initial native measurement", !(good with { ActiveSensorRead = false }).Passed);
        check("Soak rejects missing resumed native measurement", !(good with { ResumedSensorRead = false }).Passed);
        check("Soak rejects continued hidden sampling", !(good with { HiddenSamplingStopped = false }).Passed);
        check("Soak rejects live helpers", !(good with { HelpersStopped = false }).Passed);
        check("Soak rejects live sampling loops", !(good with { LoopsStopped = false }).Passed);
        var reading = HardwareReading.Empty with { MonotonicTimestamp = 1, Gpus = [new("gpu", "fixture", 25, null, null, "fixture")] };
        check("Soak accepts GPU load without requiring CPU temperature", HasNativeMeasurement(reading));
        check("Soak rejects empty native data", !HasNativeMeasurement(HardwareReading.Empty));
        check("Soak rejects errored native data", !HasNativeMeasurement(reading with { Error = "fixture" }));
        check("Soak rejects nonfinite native data", !HasNativeMeasurement(HardwareReading.Empty with { MonotonicTimestamp = 1, CpuTemp = float.NaN }));
    }

    internal static int Run(string output, string executable)
    {
        var providers = new ConcurrentBag<SensorProcessProvider>();
        using var monitor = new HardwareMonitorService(() =>
        {
            var provider = new SensorProcessProvider(executable); providers.Add(provider); return provider;
        }, hiddenProviderGraceMs: 3000); // Shorter than the first 5-second hidden sample, so quiescence is still measured.
        var settings = new AppSettings { ShowMonitor = true, EcoMode = true, SuspendMonitorWhenHidden = true, HardwareSensorsEnabled = true, MonitorUpdateIntervalMs = 1500 };
        var samples = new List<object>();
        var observed = new Dictionary<int, Process>();
        using var parent = Process.GetCurrentProcess();
        var clock = Stopwatch.StartNew();
        double parentCpuStart = parent.TotalProcessorTime.TotalSeconds;
        long frames = 0, errors = 0;
        monitor.HardwareUpdated += frame =>
        {
            Interlocked.Increment(ref frames);
            if (frame.Hardware.Error != null) Interlocked.Increment(ref errors);
        };
        bool activeRead = false, resumedRead = false, hiddenStopped = true;
        long hiddenWindows = 0, hiddenNetwork = 0, resumeTimestamp = 0;
        try
        {
            monitor.Configure(settings, true); monitor.Start();
            for (int i = 0; i < 12; i++)
            {
                if (i == 5) monitor.Configure(settings, false);
                if (i == 9) { resumeTimestamp = Stopwatch.GetTimestamp(); monitor.Configure(settings, true); }
                Thread.Sleep(5000);
                parent.Refresh();
                long childWorking = 0, childPrivate = 0; double childCpu = 0; int children = 0;
                foreach (var provider in providers)
                {
                    try
                    {
                        var child = provider.DiagnosticProcess;
                        if (child == null || child.HasExited) continue;
                        child.Refresh(); children++;
                        if (!observed.ContainsKey(child.Id))
                        {
                            var handle = Process.GetProcessById(child.Id);
                            try
                            {
                                _ = handle.Handle; // retain an independent process handle across provider disposal
                                observed.Add(child.Id, handle);
                            }
                            catch { handle.Dispose(); throw; }
                        }
                        childWorking += child.WorkingSet64; childPrivate += child.PrivateMemorySize64;
                        childCpu += child.TotalProcessorTime.TotalSeconds;
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                }
                var snapshot = monitor.GetSnapshot();
                if (i < 5 && HasNativeMeasurement(snapshot.Hardware)) activeRead = true;
                if (i >= 9 && HasNativeMeasurement(snapshot.Hardware) && snapshot.Hardware.MonotonicTimestamp >= resumeTimestamp) resumedRead = true;
                // Allow the first hidden interval to drain in-flight work, then require quiescence.
                if (i == 5) { hiddenWindows = monitor.WindowsSampleCount; hiddenNetwork = monitor.NetworkSampleCount; }
                if (i > 5 && i < 9) hiddenStopped &= monitor.WindowsSampleCount == hiddenWindows
                    && monitor.NetworkSampleCount == hiddenNetwork && children == 0;
                samples.Add(new
                {
                    Seconds = clock.Elapsed.TotalSeconds,
                    Phase = i < 5 ? "active" : i < 9 ? "hidden" : "resumed",
                    Frames = snapshot.Sequence,
                    ParentWorkingMiB = parent.WorkingSet64 / 1048576d,
                    ParentPrivateMiB = parent.PrivateMemorySize64 / 1048576d,
                    ParentCpuSeconds = parent.TotalProcessorTime.TotalSeconds - parentCpuStart,
                    SensorWorkingMiB = childWorking / 1048576d,
                    SensorPrivateMiB = childPrivate / 1048576d,
                    SensorCpuSeconds = childCpu,
                    SensorProcesses = children,
                    snapshot.CpuLoad,
                    snapshot.RamUsedGB,
                    snapshot.RamTotalGB,
                    snapshot.NetDownload,
                    snapshot.NetUpload,
                    snapshot.Hardware.CpuTemp,
                    snapshot.Hardware.CpuClockMHz,
                    snapshot.Hardware.GpuTemp,
                    snapshot.Hardware.Gpus,
                    snapshot.Hardware.Error
                });
                Console.WriteLine($"Sample {i + 1}/12: {snapshot.Sequence} frames, {children} sensor helper(s)");
            }
            monitor.Dispose();
            bool loopsStopped = SpinWait.SpinUntil(() => monitor.Completion.IsCompleted, 20000)
                && monitor.Completion.IsCompletedSuccessfully;
            bool stopped = observed.Count > 0 && SpinWait.SpinUntil(() => observed.Values.All(child =>
            {
                try { return child.HasExited; }
                catch (InvalidOperationException) { return true; }
            }), 20000);
            var acceptance = new Acceptance(activeRead, resumedRead, hiddenStopped, stopped, loopsStopped);
            var result = new
            {
                Completed = true,
                Acceptance = acceptance,
                Seconds = clock.Elapsed.TotalSeconds,
                Frames = frames,
                ErrorFrames = errors,
                ProvidersCreated = providers.Count,
                HelpersStopped = stopped,
                Samples = samples,
                Note = "60-second telemetry-only run: test host + owned native helpers. No WPF rendering, no Desktop scan. CPU counters are cumulative; helper counter covers currently live helpers. Not a long-duration UI benchmark."
            };
            File.WriteAllText(Path.Combine(output, "telemetry-soak.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return acceptance.Passed ? 0 : 1;
        }
        finally
        {
            monitor.Dispose();
            foreach (var child in observed.Values) child.Dispose();
        }
    }
}
