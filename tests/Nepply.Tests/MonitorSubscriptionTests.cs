using System.Diagnostics;
using Nepply.Models;
using Nepply.Services.Telemetry;
using Nepply.ViewModels;

internal static class MonitorSubscriptionTests
{
    internal static void Run(Action<string, bool> check, Action<int> pump)
    {
        var vm = new HardwareMonitorViewModel();
        var first = new Fixture(); var second = new Fixture();
        vm.Initialize(first); vm.SetActive(true);
        first.Publish(100, 91);
        var late = first.Capture();
        vm.Initialize(second); vm.SetActive(true);
        second.Publish(1, 22);
        Task.Run(() => late?.Invoke(Frame(101, 99))).GetAwaiter().GetResult();
        pump(60);
        check("Old subscription callback cannot overwrite new monitor", vm.CpuLoad == 22);
        second.Publish(3, 33); second.Publish(2, 88); pump(60);
        check("Out-of-order frame cannot replace newer display", vm.CpuLoad == 33);
        vm.SetActive(false);
        second.Publish(4, 44); pump(60);
        check("Hidden monitor retains display without applying pending frame", vm.CpuLoad == 33);
        second.Current = Frame(2, 88); vm.SetActive(true); pump(60);
        check("Reactivation flushes newest pending sample not older snapshot", vm.CpuLoad == 44);
        second.Publish(5, 55); vm.SetActive(false); pump(60);
        vm.SetActive(true); pump(60);
        check("Queued frame survives hide then reveal", vm.CpuLoad == 55);
        int notifications = 0;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == null) notifications++; };
        for (int i = 6; i < 1006; i++) second.Publish(i, 60);
        pump(60);
        check("Burst coalesces to one dispatcher display update", notifications == 1 && vm.CpuLoad == 60);
        var disposed = second.Capture(); vm.Cleanup();
        Task.Run(() => disposed?.Invoke(Frame(2000, 99))).GetAwaiter().GetResult(); pump(60);
        check("Cleanup rejects late samples and clears display", vm.CpuLoadText == "—" && vm.Gpus.Count == 0);
        check("Cleanup detaches monitor subscriber", second.Capture() == null);
        var gpuSource = new Fixture
        {
            Current = Frame(1, 20) with
            {
                Hardware = HardwareReading.Empty with
                { Gpus = [new("one", "First", 10, 40, 1000, "fixture"), new("two", "Second", 20, 50, 1200, "fixture")], VisibleGpus = [new("one", "First", 10, 40, 1000, "fixture")] }
            }
        };
        vm.Initialize(gpuSource); vm.SetActive(true); pump(60);
        vm.PreviewConfiguration(new AppSettings { GpuDisplay = GpuDisplayMode.Primary, GpuIdentifier = "two" });
        check("GPU preview uses full cached inventory", vm.Gpus.Count == 1 && vm.Gpus[0].Id == "two");
        vm.PreviewConfiguration(null);
        check("GPU rollback restores committed selection", vm.Gpus.Count == 1 && vm.Gpus[0].Id == "one");
        vm.Cleanup();
    }
    private static HardwareInfoSnapshot Frame(long sequence, float load) => HardwareInfoSnapshot.Empty with
    { Sequence = sequence, MonotonicTimestamp = Stopwatch.GetTimestamp(), CpuLoad = load, RamLoad = 50 };
    private sealed class Fixture : IHardwareMonitor
    {
        public HardwareInfoSnapshot Current = HardwareInfoSnapshot.Empty;
        public event Action<HardwareInfoSnapshot>? HardwareUpdated;
        internal Action<HardwareInfoSnapshot>? Capture() => HardwareUpdated;
        internal void Publish(long sequence, float load) { Current = Frame(sequence, load); HardwareUpdated?.Invoke(Current); }
        public HardwareInfoSnapshot GetSnapshot() => Current;
        public void Configure(AppSettings settings, bool visible) { }
        public void Start() { }
        public void Dispose() { }
    }
}
