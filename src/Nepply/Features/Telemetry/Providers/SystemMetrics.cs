using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
namespace Nepply.Services;

public sealed class SystemMetrics
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong Total, Available, PageTotal, PageAvailable, VirtualTotal, VirtualAvailable, Extended;
    }
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll")] private static extern bool GetPhysicallyInstalledSystemMemory(out ulong kilobytes);
    public static double? ReadInstalledMemory() => GetPhysicallyInstalledSystemMemory(out var kb) && kb > 0 ? kb / (1024d * 1024) : null;
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    private (ulong Idle, ulong Total)? _previousCpu;
    private (string Id, long Sent, long Received, long Timestamp)? _previousNetwork;
    private NetworkInterface[] _networks = [];
    private long _lastInventory;
    internal long NetworkSampleCount { get; private set; }

    public float? ReadCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return null;
        ulong total = kernel + user;
        var previous = _previousCpu; _previousCpu = (idle, total);
        if (previous == null || total <= previous.Value.Total || idle < previous.Value.Idle) return null;
        return (float)Math.Clamp(100d * (1d - (double)(idle - previous.Value.Idle) / (total - previous.Value.Total)), 0, 100);
    }
    public static (double? Used, double? Total, float? Load) ReadMemory()
    {
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref memory) || memory.Total == 0 || memory.Available > memory.Total) return (null, null, null);
        const double gib = 1024d * 1024 * 1024;
        return ((memory.Total - memory.Available) / gib, memory.Total / gib,
            (float)(100d * (memory.Total - memory.Available) / memory.Total));
    }
    // Status is a resource key, set only when no adapter name can be shown.
    public (double? Upload, double? Download, string Name, string? Status) ReadNetwork(string selectedId, bool enabled = true)
    {
        if (!enabled)
        {
            _previousNetwork = null;
            _networks = [];
            _lastInventory = 0;
            return (null, null, "", null);
        }
        NetworkSampleCount++;
        try
        {
            long now = Stopwatch.GetTimestamp();
            if (_networks.Length == 0 || Stopwatch.GetElapsedTime(_lastInventory, now).TotalSeconds >= 10)
            { _networks = NetworkInterface.GetAllNetworkInterfaces(); _lastInventory = now; }
            var adapter = !string.IsNullOrEmpty(selectedId)
                ? _networks.FirstOrDefault(n => n.Id == selectedId && n.OperationalStatus == OperationalStatus.Up)
                : _networks.Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
                    .Where(n => !n.Description.Contains("virtual", StringComparison.OrdinalIgnoreCase) && !n.Name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(n => n.GetIPProperties().GatewayAddresses.Count > 0).ThenByDescending(n => n.Speed).FirstOrDefault();
            if (adapter == null) { _previousNetwork = null; return (null, null, "", "NoNetwork"); }
            var stats = adapter.GetIPStatistics(); var previous = _previousNetwork;
            _previousNetwork = (adapter.Id, stats.BytesSent, stats.BytesReceived, now);
            if (previous == null || previous.Value.Id != adapter.Id) return (null, null, adapter.Name, null);
            double elapsed = Stopwatch.GetElapsedTime(previous.Value.Timestamp, now).TotalSeconds;
            return (CalculateRate(stats.BytesSent, previous.Value.Sent, elapsed), CalculateRate(stats.BytesReceived, previous.Value.Received, elapsed), adapter.Name, null);
        }
        catch (Exception ex) { _previousNetwork = null; AppLogger.Error(ex, "Network sample"); return (null, null, "", "NetworkReadError"); }
    }
    public static double? CalculateRate(long current, long previous, double elapsed) =>
        elapsed is <= 0 or > 30 || current < previous || !double.IsFinite(elapsed) ? null : (current - previous) / elapsed;
}
