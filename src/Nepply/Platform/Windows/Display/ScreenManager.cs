using System.Runtime.InteropServices;
namespace Nepply.Helpers;

public sealed record DisplayScreen(string Device, int Left, int Top, int Right, int Bottom, double Scale, bool Primary)
{
    public string Name { get; init; } = "";
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
    // Left, Top, Right and Bottom are the work area; this is the whole monitor, taskbar included.
    public (int Left, int Top, int Right, int Bottom) Monitor { get; init; } = (Left, Top, Right, Bottom);
    public bool OnMonitor(int x, int y) => x >= Monitor.Left && x < Monitor.Right && y >= Monitor.Top && y < Monitor.Bottom;
}
public static class ScreenManager
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    { public int Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint LowPart; public int HighPart; }
    [StructLayout(LayoutKind.Sequential)] private struct Rational { public uint Numerator, Denominator; }
    [StructLayout(LayoutKind.Sequential)] private struct PathSourceInfo { public Luid AdapterId; public uint Id, ModeInfoIdx, StatusFlags; }
    [StructLayout(LayoutKind.Sequential)]
    private struct PathTargetInfo
    {
        public Luid AdapterId; public uint Id, ModeInfoIdx, OutputTechnology, Rotation, Scaling;
        public Rational RefreshRate; public uint ScanLineOrdering; public int TargetAvailable; public uint StatusFlags;
    }
    [StructLayout(LayoutKind.Sequential)] private struct DisplayConfigPathInfo { public PathSourceInfo SourceInfo; public PathTargetInfo TargetInfo; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct DisplayConfigModeInfo { public uint InfoType, Id; public Luid AdapterId; public ulong M0, M1, M2, M3, M4, M5; }
    [StructLayout(LayoutKind.Sequential)] private struct DeviceInfoHeader { public uint Type, Size; public Luid AdapterId; public uint Id; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SourceDeviceName
    { public DeviceInfoHeader Header; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string ViewGdiDeviceName; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TargetDeviceName
    {
        public DeviceInfoHeader Header; public uint Flags, OutputTechnology; public ushort EdidManufactureId, EdidProductCodeId; public uint ConnectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string MonitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string MonitorDevicePath;
    }
    private delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPath, out uint numMode);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint numPath, [Out] DisplayConfigPathInfo[] paths, ref uint numMode, [Out] DisplayConfigModeInfo[] modes, IntPtr currentTopologyId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DisplayConfigGetDeviceInfo(ref SourceDeviceName packet);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DisplayConfigGetDeviceInfo(ref TargetDeviceName packet);
    internal static DisplayScreen? ForWindow(IntPtr window)
    {
        if (window == IntPtr.Zero) return null;
        var monitor = MonitorFromWindow(window, 2); // MONITOR_DEFAULTTONEAREST
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), Device = "" };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return null;
        return GetScreens().FirstOrDefault(s => string.Equals(s.Device, info.Device, StringComparison.OrdinalIgnoreCase));
    }
    public static List<DisplayScreen> GetScreens()
    {
        var names = ReadMonitorNames();
        var result = new List<DisplayScreen>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), Device = "" };
            if (GetMonitorInfo(monitor, ref info))
            {
                double scale = GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 && dpi > 0 ? dpi / 96d : 1;
                if (info.Work.Right > info.Work.Left && info.Work.Bottom > info.Work.Top)
                    result.Add(new(info.Device, info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom, scale, (info.Flags & 1) != 0)
                    {
                        Name = names.TryGetValue(info.Device, out var name) ? name : "",
                        Monitor = (info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom)
                    });
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }
    private static Dictionary<string, string> ReadMonitorNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            const uint activePaths = 2; // QDC_ONLY_ACTIVE_PATHS
            if (GetDisplayConfigBufferSizes(activePaths, out uint pathCount, out uint modeCount) != 0
                || pathCount is 0 or > 256 || modeCount > 1024) return names;
            var paths = new DisplayConfigPathInfo[pathCount];
            var modes = new DisplayConfigModeInfo[modeCount];
            if (QueryDisplayConfig(activePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0) return names;
            uint sourceSize = (uint)Marshal.SizeOf<SourceDeviceName>();
            uint targetSize = (uint)Marshal.SizeOf<TargetDeviceName>();
            for (uint i = 0; i < pathCount && i < paths.Length; i++)
            {
                var source = new SourceDeviceName
                {
                    Header = new DeviceInfoHeader { Type = 1, Size = sourceSize, AdapterId = paths[i].SourceInfo.AdapterId, Id = paths[i].SourceInfo.Id },
                    ViewGdiDeviceName = ""
                };
                var target = new TargetDeviceName
                {
                    Header = new DeviceInfoHeader { Type = 2, Size = targetSize, AdapterId = paths[i].TargetInfo.AdapterId, Id = paths[i].TargetInfo.Id },
                    MonitorFriendlyDeviceName = "",
                    MonitorDevicePath = ""
                };
                if (DisplayConfigGetDeviceInfo(ref source) != 0 || DisplayConfigGetDeviceInfo(ref target) != 0) continue;
                string device = source.ViewGdiDeviceName?.Trim() ?? "";
                string friendly = target.MonitorFriendlyDeviceName?.Trim() ?? "";
                if (device.Length > 0 && friendly.Length > 0 && !names.ContainsKey(device))
                    names[device] = friendly;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Services.AppLogger.Error(ex, "DisplayConfig monitor names");
        }
        return names;
    }
    public static DisplayScreen Select(string device)
    {
        return SelectAvailable(GetScreens(), device) ?? throw new InvalidOperationException("Windows reported no usable display.");
    }
    internal static DisplayScreen? SelectAvailable(IEnumerable<DisplayScreen> screens, string device)
    {
        var valid = screens.Where(s => s.Width > 0 && s.Height > 0 && double.IsFinite(s.Scale) && s.Scale > 0).ToArray();
        return valid.FirstOrDefault(s => string.Equals(s.Device, device, StringComparison.OrdinalIgnoreCase))
            ?? valid.FirstOrDefault(s => s.Primary) ?? valid.FirstOrDefault();
    }
}
