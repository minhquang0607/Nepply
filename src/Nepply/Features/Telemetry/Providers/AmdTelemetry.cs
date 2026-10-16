using System.Runtime.InteropServices;
namespace Nepply.Services;

/// <summary>Read-only AMD ADL PMLog fallback, owned by the native collector thread.
/// LHM 0.9.6 gpu-amd identifiers contain ADL AdapterIndex (not list position).</summary>
public sealed class AmdTelemetry : IDisposable
{
    public sealed record Sample(float? GpuTemperature, float? CpuTemperature, float? CpuClockMHz);
    private IntPtr _context;
    private bool _unavailable;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Allocate(int size);
    private readonly Allocate _allocate = Marshal.AllocHGlobal;
    [StructLayout(LayoutKind.Sequential)] private struct Sensor { public int Supported; public int Value; }
    [StructLayout(LayoutKind.Sequential)] private struct LogData
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public Sensor[] Sensors;
    }
    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int ADL2_Main_Control_Create(Allocate allocate, int connected, ref IntPtr context);
    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int ADL2_Main_Control_Destroy(IntPtr context);
    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int ADL2_New_QueryPMLogData_Get(IntPtr context, int adapter, ref LogData data);
    public static float? ValidateMetric(int supported, int value, float minimum, float maximum) => supported != 0 ? Nepply.Models.MetricValue.Valid(value, minimum, maximum) : null;
    public Sample? Read(string hardwareId)
    {
        if (_unavailable || !hardwareId.StartsWith("/gpu-amd/", StringComparison.Ordinal) || !int.TryParse(hardwareId[9..], out int adapter) || adapter < 0) return null;
        try
        {
            if (_context == IntPtr.Zero && ADL2_Main_Control_Create(_allocate,1,ref _context) != 0) { Dispose(); _unavailable = true; return null; }
            if (_context == IntPtr.Zero) return null;
            var data = new LogData { Size = Marshal.SizeOf<LogData>(), Sensors = new Sensor[256] };
            if (ADL2_New_QueryPMLogData_Get(_context,adapter,ref data) != 0) return null;
            float? ReadSensor(int index, int min, int max) => ValidateMetric(data.Sensors[index].Supported, data.Sensors[index].Value,min,max);
            // ADL_PMLOG_TEMPERATURE_EDGE=8, TEMPERATURE_CPU=32, CLK_CPUCLK=34.
            return new(ReadSensor(8,1,130), ReadSensor(32,1,130), ReadSensor(34,1,15000));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { _unavailable = true; AppLogger.Error(ex,"AMD PMLog unavailable"); return null; }
    }
    public void Dispose()
    {
        if (_context != IntPtr.Zero) { ADL2_Main_Control_Destroy(_context); _context = IntPtr.Zero; }
        GC.KeepAlive(_allocate);
    }
}
