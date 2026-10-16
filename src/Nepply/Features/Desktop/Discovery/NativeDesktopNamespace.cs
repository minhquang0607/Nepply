using System.IO;
using System.Runtime.InteropServices;
using static Nepply.Helpers.NativeInterop;

namespace Nepply.Modules.Desktop;

/// <summary>STA-owned Shell enumeration; no UI, snapshot publication or filesystem policy.</summary>
internal static class NativeDesktopNamespace
{
    internal sealed record Entry(string Path, string Name);

    internal static IReadOnlyList<Entry> Read(bool includeHidden)
    {
        IShellFolder? desktop = null;
        IEnumIdList? enumerator = null;
        var entries = new List<Entry>();
        try
        {
            Marshal.ThrowExceptionForHR(SHGetDesktopFolder(out desktop));
            if (desktop == null) throw new IOException("Shell returned no Desktop folder.");
            desktop.EnumObjects(IntPtr.Zero, 0x20u | 0x40u | (includeHidden ? 0x80u | 0x10000u : 0), out var enumPointer);
            // EnumObjects permits a null enumerator for an empty successful folder.
            if (enumPointer == IntPtr.Zero) return entries;
            try { enumerator = (IEnumIdList)Marshal.GetObjectForIUnknown(enumPointer); }
            finally { Marshal.Release(enumPointer); }
            while (true)
            {
                int result = enumerator.Next(1, out var child, out uint fetched);
                Marshal.ThrowExceptionForHR(result);
                if (fetched == 0) break;
                IShellItem? item = null;
                try
                {
                    if (fetched != 1 || child == IntPtr.Zero) throw new IOException("Shell returned an invalid Desktop entry.");
                    var iid = typeof(IShellItem).GUID;
                    Marshal.ThrowExceptionForHR(SHCreateItemWithParent(IntPtr.Zero, desktop, child, ref iid, out item));
                    if (item == null) throw new IOException("Shell returned no Desktop item.");
                    string displayName = ReadName(item, 0);
                    try
                    {
                        string identity;
                        try { identity = ReadName(item, 0x80028000); }
                        catch (FileNotFoundException)
                        {
                            // A virtual item may have no resolvable filesystem destination.
                            // Its parent-relative parsing name is relative to Desktop here;
                            // retain that identity for the caller's Desktop-only filtering.
                            identity = ReadName(item, 0x80018001);
                        }
                        entries.Add(new(identity, displayName));
                    }
                    catch (Exception ex) { throw new IOException("Shell could not read the Desktop identity of " + displayName, ex); }
                }
                finally
                {
                    if (item != null) Marshal.ReleaseComObject(item);
                    if (child != IntPtr.Zero) Marshal.FreeCoTaskMem(child);
                }
                if (entries.Count > Services.DesktopImageBudget.MaximumEntries) throw new IOException("Desktop namespace exceeds the item limit.");
            }
            return entries;
        }
        finally
        {
            if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            if (desktop != null) Marshal.ReleaseComObject(desktop);
        }
    }

    private static string ReadName(IShellItem item, uint kind)
    {
        IntPtr text = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(item.GetDisplayName(kind, out text));
            var value = Marshal.PtrToStringUni(text);
            if (string.IsNullOrWhiteSpace(value) || value.Length > 32768)
                throw new IOException("Shell returned an invalid Desktop name.");
            return value;
        }
        finally { if (text != IntPtr.Zero) Marshal.FreeCoTaskMem(text); }
    }

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHGetDesktopFolder([MarshalAs(UnmanagedType.Interface)] out IShellFolder? folder);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHCreateItemWithParent(IntPtr parentId, IShellFolder parent, IntPtr childId,
        ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItem? item);

    [ComImport, Guid("000214F2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumIdList
    {
        [PreserveSig] int Next(uint count, out IntPtr child, out uint fetched);
        [PreserveSig] int Skip(uint count);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumIdList copy);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IntPtr value);
        [PreserveSig] int GetParent(out IShellItem parent);
        [PreserveSig] int GetDisplayName(uint kind, out IntPtr text);
        [PreserveSig] int GetAttributes(uint mask, out uint attributes);
        [PreserveSig] int Compare(IShellItem other, uint hints, out int order);
    }
}
