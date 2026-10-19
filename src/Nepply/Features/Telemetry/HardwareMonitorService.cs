using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using Nepply.Models;
using Nepply.Services.Telemetry;
namespace Nepply.Services;

/// <summary>Independent Windows and native-sensor loops. Each Computer has exactly one owner.</summary>
public sealed class HardwareMonitorService : IHardwareMonitor
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _lifecycle = new();
    private Task? _fastTask, _sensorTask;
    private AppSettings _settings;
    private volatile bool _visible;
    private bool _disposed;
    private HardwareInfoSnapshot _snapshot = HardwareInfoSnapshot.Empty;
    private HardwareReading _hardware = HardwareReading.Empty;
    private long _sequence;
    private readonly SamplingWake _fastWake = new(), _sensorWake = new();
    private long _windowsSamples;
    private long _networkSamples;
    internal long WindowsSampleCount => Interlocked.Read(ref _windowsSamples);
    internal long NetworkSampleCount => Interlocked.Read(ref _networkSamples);
    internal Task Completion { get { lock (_lifecycle) return Task.WhenAll(_fastTask ?? Task.CompletedTask,_sensorTask ?? Task.CompletedTask); } }
    public event Action<HardwareInfoSnapshot>? HardwareUpdated;
    private readonly Func<IHardwareProvider> _providerFactory;
    public HardwareMonitorService(int intervalMs = 1500) : this(() => new LibreHardwareProvider(), intervalMs) { }
    public HardwareMonitorService(Func<IHardwareProvider> providerFactory, int intervalMs = 1500)
    { _providerFactory = providerFactory; _settings = new() { MonitorUpdateIntervalMs = intervalMs }; }
    public void Configure(AppSettings settings, bool visible)
    {
        var configuration = settings.Clone(); configuration.Validate();
        lock (_lifecycle)
        {
            if (_disposed) return;
            Volatile.Write(ref _settings, configuration); _visible = visible;
            _fastWake.Signal(); _sensorWake.Signal();
        }
    }
    public HardwareInfoSnapshot GetSnapshot() => Volatile.Read(ref _snapshot);
    public void Start()
    {
        lock (_lifecycle)
        {
            if (_disposed || _fastTask != null) return;
            _fastTask = Task.Run(() => SampleWindowsAsync(_shutdown.Token));
            _sensorTask = Task.Run(() => SampleHardwareAsync(_shutdown.Token));
        }
    }
    private int Period(AppSettings settings) => settings.EcoMode && !_visible
        ? Math.Max(5000, settings.MonitorUpdateIntervalMs) : settings.MonitorUpdateIntervalMs;
    private async Task SampleWindowsAsync(CancellationToken token)
    {
        SystemMetrics? system = null;
        double? installedMemory = null;
        try
        {
            while (!token.IsCancellationRequested)
            {
                _fastWake.ConsumePending();
                var settings = Volatile.Read(ref _settings);
                if (!settings.ShowMonitor || (settings.SuspendMonitorWhenHidden && !_visible))
                {
                    system = null; // Reset delta baselines when collection resumes.
                    await _fastWake.WaitAsync(Timeout.Infinite,token).ConfigureAwait(false);
                    continue;
                }
                try
                {
                    if (system == null) { system = new(); installedMemory = SystemMetrics.ReadInstalledMemory(); }
                    Interlocked.Increment(ref _windowsSamples);
                    float? cpu = system.ReadCpu();
                    var ram = SystemMetrics.ReadMemory();
                    long networkBefore = system.NetworkSampleCount;
                    var net = system.ReadNetwork(settings.NetworkInterfaceId,settings.ShowNetwork);
                    Interlocked.Add(ref _networkSamples,system.NetworkSampleCount-networkBefore);
                    var native = Volatile.Read(ref _hardware);
                    if (!settings.HardwareSensorsEnabled) native = HardwareReading.Empty with { Error = "SensorDisabled" };
                    else native = TelemetryFreshness.ExpireIfNeeded(native,Period(settings));
                    var snapshot = new HardwareInfoSnapshot(Interlocked.Increment(ref _sequence), DateTimeOffset.UtcNow,
                        cpu, ram.Used, ram.Total, ram.Load, net.Upload, net.Download, net.Name,
                        native with { VisibleGpus = SelectGpus(native.Gpus, settings) }, []) { RamInstalledGB = installedMemory, NetworkStatus = net.Status };
                    Volatile.Write(ref _snapshot, snapshot);
                    if (!token.IsCancellationRequested && Volatile.Read(ref _settings).ShowMonitor)
                    {
                        try { HardwareUpdated?.Invoke(snapshot); }
                        catch (Exception ex) { AppLogger.Error(ex, "Monitor subscriber"); }
                    }
                }
                catch (Exception ex) { AppLogger.Error(ex, "Windows metrics"); }
                await _fastWake.WaitAsync(Period(settings),token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }
    private async Task SampleHardwareAsync(CancellationToken token)
    {
        IHardwareProvider? provider = null;
        int failures = 0;
        bool policyBlocked = false;
        try
        {
            while (!token.IsCancellationRequested)
            {
                _sensorWake.ConsumePending();
                var settings = Volatile.Read(ref _settings);
                int delay = Math.Max(1500, Period(settings));
                if (!settings.ShowMonitor || !settings.HardwareSensorsEnabled || (settings.SuspendMonitorWhenHidden && !_visible))
                {
                    ReleaseProvider(ref provider);
                    failures = 0;
                    policyBlocked = false;
                    Volatile.Write(ref _hardware, HardwareReading.Empty);
                    await _sensorWake.WaitAsync(Timeout.Infinite,token).ConfigureAwait(false);
                    continue;
                }
                if (policyBlocked)
                {
                    await _sensorWake.WaitAsync(Timeout.Infinite,token).ConfigureAwait(false);
                    continue;
                }
                try
                {
                    provider ??= _providerFactory();
                    var reading = provider.Read(settings,token);
                    var current = Volatile.Read(ref _settings);
                    if (current.ShowMonitor && current.HardwareSensorsEnabled && !token.IsCancellationRequested)
                        Volatile.Write(ref _hardware, reading);
                    failures = 0;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    policyBlocked = HelperFailure.IsPolicyBlocked(ex);
                    var unavailable = TelemetryFreshness.Unavailable(Volatile.Read(ref _hardware),policyBlocked ? HelperFailure.PolicyMessageKey : "SensorReconnecting");
                    Volatile.Write(ref _hardware,policyBlocked ? unavailable with { MonotonicTimestamp=0 } : unavailable);
                    AppLogger.Error(ex, "Telemetry provider");
                    ReleaseProvider(ref provider);
                    delay = (int)(1000 * Math.Min(60, 5 * Math.Pow(2, Math.Min(++failures,4))));
                }
                await _sensorWake.WaitAsync(delay,token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            ReleaseProvider(ref provider);
        }
    }
    private static void ReleaseProvider(ref IHardwareProvider? provider)
    {
        // Detach first: a faulty driver must not leave a disposed instance available for reuse.
        var previous = provider;
        provider = null;
        try { previous?.Dispose(); }
        catch (Exception ex) { AppLogger.Error(ex, "Provider cleanup"); }
    }
    public static ImmutableArray<GpuReading> SelectGpus(ImmutableArray<GpuReading> gpus, AppSettings settings)
    {
        var selected = settings.GpuDisplay switch
        {
            GpuDisplayMode.Primary => gpus.Where(g => g.Id == settings.GpuIdentifier),
            GpuDisplayMode.Secondary => gpus.Where(g => g.Id == settings.IntegratedGpuIdentifier),
            _ => gpus.AsEnumerable()
        };
        // Never silently substitute another adapter when an explicit selection disappears.
        string Category(GpuReading g) => g.Id == settings.IntegratedGpuIdentifier ? "iGPU"
            : g.Id == settings.GpuIdentifier ? "GPU" : g.Kind == GpuKind.Integrated ? "iGPU" : "GPU";
        return selected.Select(g =>
        {
            string category = Category(g);
            var peers = gpus.Where(other => Category(other) == category).ToArray();
            return g with { Label = peers.Length > 1 ? category + " " + (Array.IndexOf(peers, g) + 1) : category };
        }).ToImmutableArray();
    }

    public void Stop() => Dispose();
    public void Dispose()
    {
        lock (_lifecycle)
        {
            if (_disposed) return;
            _disposed = true;
            var cancellation = _shutdown.CancelAsync();
            HardwareUpdated = null;
            // Never close Computer concurrently or block the UI on a native call.
            _ = Task.WhenAll(cancellation, _fastTask ?? Task.CompletedTask, _sensorTask ?? Task.CompletedTask)
                .ContinueWith(_ => { _shutdown.Dispose(); _fastWake.Dispose(); _sensorWake.Dispose(); }, TaskScheduler.Default);
        }
    }
}
