using Nepply.Helpers;
using Nepply.Services;
using Nepply.Services.Telemetry;

namespace Nepply.Modules;

/// <summary>Owned services for one window lifetime. Construction does not start sampling.</summary>
internal sealed class WindowServices : IDisposable
{
    internal IHardwareMonitor Monitor { get; }
    internal DesktopIconService Desktop { get; }
    private bool _disposed;
    private WindowServices(IHardwareMonitor monitor, DesktopIconService desktop)
        => (Monitor, Desktop) = (monitor, desktop);

    internal static WindowServices Create(bool preview, bool smokeTest,
        IHardwareMonitor? monitor = null, DesktopIconService? desktop = null)
        => Create(() => monitor ?? new HardwareMonitorService(() => new SensorProcessProvider()),
            () => desktop ?? new DesktopIconService(preview && !smokeTest ? null : Environment.ProcessPath));

    internal static WindowServices Create(Func<IHardwareMonitor> monitorFactory, Func<DesktopIconService> desktopFactory)
    {
        ArgumentNullException.ThrowIfNull(monitorFactory);
        ArgumentNullException.ThrowIfNull(desktopFactory);
        var monitor = monitorFactory() ?? throw new InvalidOperationException("Monitor factory returned null.");
        try { return new(monitor, desktopFactory() ?? throw new InvalidOperationException("Desktop factory returned null.")); }
        catch
        {
            CleanupSequence.Run(("Partially constructed monitor", monitor.Dispose));
            throw;
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CleanupSequence.Run(("Monitor service", Monitor.Dispose), ("Desktop visibility and scanner", Desktop.Dispose));
    }
}
