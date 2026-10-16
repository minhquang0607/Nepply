using System.Runtime.InteropServices;
namespace Nepply.Helpers;

public sealed record DisplayScreen(string Device, int Left, int Top, int Right, int Bottom, double Scale, bool Primary)
{
    public string Label => Device.Replace(@"\\.\", "") + (Primary ? " · Chính" : "");
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
}
public static class ScreenManager
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MonitorInfo
    { public int Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    private delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    internal static DisplayScreen? ForWindow(IntPtr window)
    {
        if(window==IntPtr.Zero) return null;
        var monitor=MonitorFromWindow(window,2); // MONITOR_DEFAULTTONEAREST
        var info=new MonitorInfo {Size=Marshal.SizeOf<MonitorInfo>(),Device=""};
        if(monitor==IntPtr.Zero || !GetMonitorInfo(monitor,ref info)) return null;
        return GetScreens().FirstOrDefault(s=>string.Equals(s.Device,info.Device,StringComparison.OrdinalIgnoreCase));
    }
    public static List<DisplayScreen> GetScreens()
    {
        var result = new List<DisplayScreen>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), Device = "" };
            if (GetMonitorInfo(monitor, ref info))
            {
                double scale = GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 && dpi>0 ? dpi / 96d : 1;
                if(info.Work.Right>info.Work.Left && info.Work.Bottom>info.Work.Top)
                    result.Add(new(info.Device, info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom, scale, (info.Flags & 1) != 0));
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }
    public static DisplayScreen Select(string device)
    {
        return SelectAvailable(GetScreens(),device) ?? throw new InvalidOperationException("Windows chưa cung cấp màn hình khả dụng.");
    }
    internal static DisplayScreen? SelectAvailable(IEnumerable<DisplayScreen> screens,string device)
    {
        var valid=screens.Where(s=>s.Width>0 && s.Height>0 && double.IsFinite(s.Scale) && s.Scale>0).ToArray();
        return valid.FirstOrDefault(s=>string.Equals(s.Device,device,StringComparison.OrdinalIgnoreCase))
            ?? valid.FirstOrDefault(s=>s.Primary) ?? valid.FirstOrDefault();
    }
}
