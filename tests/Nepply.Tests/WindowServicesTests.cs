using Nepply.Models;
using Nepply.Modules;
using Nepply.Services;
using Nepply.Services.Telemetry;

internal static class WindowServicesTests
{
    internal static void Run(Action<string,bool> check)
    {
        var monitor = new Monitor();
        var failure = new InvalidOperationException("Desktop factory fixture failure");
        Exception? observed = null;
        try { WindowServices.Create(() => monitor, () => throw failure); }
        catch (Exception ex) { observed = ex; }
        check("Composition releases monitor after desktop construction fails", monitor.Disposals == 1 && ReferenceEquals(observed, failure));
        monitor = new Monitor { FailDispose = true };
        try { WindowServices.Create(() => monitor, () => throw failure); }
        catch (Exception ex) { observed = ex; }
        check("Cleanup failure does not mask construction failure", monitor.Disposals == 1 && ReferenceEquals(observed, failure));
        bool desktopCreated = false;
        try { WindowServices.Create(() => throw failure, () => { desktopCreated = true; return new DesktopIconService(); }); }
        catch (InvalidOperationException) { }
        check("Monitor construction failure never starts desktop factory", !desktopCreated);
        monitor = new Monitor();
        int scans = 0;
        var desktop = new DesktopIconService(_ => { scans++; return Task.FromResult(new List<DesktopIcon>()); });
        var services = WindowServices.Create(() => monitor, () => desktop);
        check("Composition retains exact injected services without starting work", ReferenceEquals(services.Monitor,monitor)
            && ReferenceEquals(services.Desktop,desktop) && monitor.Starts == 0 && scans == 0);
        monitor.FailDispose = true;
        services.Dispose(); services.Dispose();
        desktop.ScanDesktopIconsAsync().GetAwaiter().GetResult();
        check("Disposal continues to desktop and is idempotent", monitor.Disposals == 1 && scans == 0);
    }
    private sealed class Monitor : IHardwareMonitor
    {
        internal int Disposals, Starts;
        internal bool FailDispose;
        public event Action<HardwareInfoSnapshot>? HardwareUpdated { add { } remove { } }
        public void Configure(AppSettings settings,bool visible) { }
        public HardwareInfoSnapshot GetSnapshot() => HardwareInfoSnapshot.Empty;
        public void Start() => Starts++;
        public void Dispose() { Disposals++; if (FailDispose) throw new InvalidOperationException("Fixture disposal failure"); }
    }
}
