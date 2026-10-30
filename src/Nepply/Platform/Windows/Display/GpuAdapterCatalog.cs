using System.Runtime.InteropServices;
using Nepply.Models;

namespace Nepply.Services.Telemetry;

internal sealed record GpuAdapterIdentity(string Name, GpuKind Kind);

// DXCore reports IsIntegrated directly. Do not infer GPU type from brand or VRAM size.
// This snapshot owns no COM objects after Read returns and runs only on inventory changes.
internal static class GpuAdapterCatalog
{
    internal static GpuKind Classify(string name, IReadOnlyList<GpuAdapterIdentity> adapters)
    {
        var key = Normalize(name);
        if (key.Length == 0) return GpuKind.Unknown;
        var matches = adapters.Where(a => Normalize(a.Name) == key).Select(a => a.Kind).Distinct().ToArray();
        // Identical names with contradictory hardware facts cannot be mapped safely.
        return matches.Length == 1 ? matches[0] : GpuKind.Unknown;
    }

    private static string Normalize(string name) => string.Concat(name
        .Replace("(R)", "", StringComparison.OrdinalIgnoreCase)
        .Replace("(TM)", "", StringComparison.OrdinalIgnoreCase)
        .Replace("®", "").Replace("™", "").Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    internal static IReadOnlyList<GpuAdapterIdentity> Read()
    {
        var result = new List<GpuAdapterIdentity>();
        IAdapterFactory? factory = null;
        IAdapterList? list = null;
        try
        {
            var factoryId = typeof(IAdapterFactory).GUID;
            if (DXCoreCreateAdapterFactory(ref factoryId, out factory) < 0 || factory == null) return result;
            // D3D11 includes older graphics adapters as well as D3D12-capable GPUs.
            var graphics = new Guid("8c47866b-7583-450d-f0f0-6bada895af4b");
            var listId = typeof(IAdapterList).GUID;
            if (factory.CreateAdapterList(1, ref graphics, ref listId, out list) < 0 || list == null) return result;
            uint count = Math.Min(list.GetAdapterCount(), 64);
            for (uint index = 0; index < count; index++)
            {
                IAdapter? adapter = null;
                try
                {
                    var adapterId = typeof(IAdapter).GUID;
                    if (list.GetAdapter(index, ref adapterId, out adapter) < 0 || adapter == null || !adapter.IsValid()) continue;
                    if (ReadBoolean(adapter, AdapterProperty.IsHardware) != true) continue;
                    string? name = ReadProperty(adapter, AdapterProperty.DriverDescription,
                        (buffer, size) => Marshal.PtrToStringUTF8(buffer, size)?.TrimEnd('\0'));
                    bool? integrated = ReadBoolean(adapter, AdapterProperty.IsIntegrated);
                    if (!string.IsNullOrWhiteSpace(name)) result.Add(new(name,
                        integrated == true ? GpuKind.Integrated : integrated == false ? GpuKind.Discrete : GpuKind.Unknown));
                }
                finally { Release(adapter); }
            }
        }
        catch (Exception ex) when (ex is COMException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            // Older Windows/unsupported drivers remain usable, with an honest generic GPU label.
            AppLogger.Error(ex, "DXCore GPU identity");
        }
        finally { Release(list); Release(factory); }
        return result;
    }

    private static bool? ReadBoolean(IAdapter adapter, AdapterProperty property)
        => ReadProperty(adapter, property, (buffer, size) => size == 1 ? Marshal.ReadByte(buffer) switch
        { 0 => (bool?)false, 1 => true, _ => null } : null);

    private static T? ReadProperty<T>(IAdapter adapter, AdapterProperty property, Func<IntPtr, int, T?> read)
    {
        if (!adapter.IsPropertySupported(property) || adapter.GetPropertySize(property, out var size) < 0 || size is 0 or > 4096)
            return default;
        var buffer = Marshal.AllocHGlobal((int)size);
        try { return adapter.GetProperty(property, size, buffer) >= 0 ? read(buffer, (int)size) : default; }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static void Release(object? value)
    {
        // RCWs are private to this call; no shared COM references are invalidated.
        if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }

    private enum AdapterProperty : uint { DriverDescription = 2, IsHardware = 11, IsIntegrated = 12 }

    [DllImport("dxcore.dll", ExactSpelling = true)]
    private static extern int DXCoreCreateAdapterFactory(ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IAdapterFactory? factory);

    [ComImport, Guid("78ee5945-c36e-4b13-a669-005dd11c0f06"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAdapterFactory
    {
        [PreserveSig] int CreateAdapterList(uint count, ref Guid attribute, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IAdapterList? list);
    }

    [ComImport, Guid("526c7776-40e9-459b-b711-f32ad76dfc28"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAdapterList
    {
        [PreserveSig] int GetAdapter(uint index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IAdapter? adapter);
        [PreserveSig] uint GetAdapterCount();
    }

    [ComImport, Guid("f0db4c7f-fe5a-42a2-bd62-f2a6cf6fc83e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAdapter
    {
        [PreserveSig][return: MarshalAs(UnmanagedType.U1)] bool IsValid();
        [PreserveSig][return: MarshalAs(UnmanagedType.U1)] bool IsAttributeSupported(ref Guid attribute);
        [PreserveSig][return: MarshalAs(UnmanagedType.U1)] bool IsPropertySupported(AdapterProperty property);
        [PreserveSig] int GetProperty(AdapterProperty property, nuint size, IntPtr buffer);
        [PreserveSig] int GetPropertySize(AdapterProperty property, out nuint size);
    }
}
