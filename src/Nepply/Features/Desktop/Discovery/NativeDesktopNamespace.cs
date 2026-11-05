using System.IO;
using System.Runtime.InteropServices;
using static Nepply.Helpers.NativeInterop;

namespace Nepply.Modules.Desktop;

/// <summary>STA-owned Shell enumeration; no UI, snapshot publication or filesystem policy.</summary>
internal static class NativeDesktopNamespace
{
    internal sealed record Entry(string Path, string Name);

    internal delegate int NextChild(out IntPtr child, out uint fetched);

    internal static IReadOnlyList<Entry> Read(bool includeHidden, Services.DesktopScanIssues issues)
    {
        IShellFolder? desktop = null;
        IEnumIdList? enumerator = null;
        try
        {
            Marshal.ThrowExceptionForHR(SHGetDesktopFolder(out desktop));
            if (desktop == null) throw new IOException("Shell returned no Desktop folder.");
            desktop.EnumObjects(IntPtr.Zero, 0x20u | 0x40u | (includeHidden ? 0x80u | 0x10000u : 0), out var enumPointer);
            // EnumObjects permits a null enumerator for an empty successful folder.
            if (enumPointer == IntPtr.Zero) return [];
            try { enumerator = (IEnumIdList)Marshal.GetObjectForIUnknown(enumPointer); }
            finally { Marshal.Release(enumPointer); }
            var live = enumerator;
            var folder = desktop;
            return Collect((out IntPtr child, out uint fetched) => live.Next(1, out child, out fetched), child => ReadChild(folder, child), issues);
        }
        finally
        {
            if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            if (desktop != null) Marshal.ReleaseComObject(desktop);
        }
    }

    internal static List<Entry> Collect(NextChild next, Func<IntPtr, Entry> readChild, Services.DesktopScanIssues issues)
    {
        var entries = new List<Entry>();
        int attempted = 0, failed = 0;
        while (true)
        {
            IntPtr child = IntPtr.Zero;
            try
            {
                // A failing enumerator cannot say what it left out, so the whole source is unreliable.
                Marshal.ThrowExceptionForHR(next(out child, out uint fetched));
                if (fetched == 0) break;
                if (fetched != 1) throw new IOException("Shell returned an invalid Desktop entry count.");
                // Skipped entries count too: an enumerator that only returns bad entries must still end.
                if (++attempted > Services.DesktopImageBudget.MaximumEntries) throw new IOException("Desktop namespace exceeds the item limit.");
                try
                {
                    if (child == IntPtr.Zero) throw new IOException("Shell returned an empty Desktop entry.");
                    entries.Add(readChild(child));
                }
                catch (Exception ex)
                {
                    failed++;
                    issues.Skip("Shell namespace item " + attempted, ex);
                }
            }
            finally { if (child != IntPtr.Zero) Marshal.FreeCoTaskMem(child); }
        }
        Services.DesktopScanIssues.RequireSomeRead(attempted, failed, "the Shell Desktop namespace");
        return entries;
    }

    private static Entry ReadChild(IShellFolder desktop, IntPtr child)
    {
        IShellItem? item = null;
        try
        {
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
                return new(identity, displayName);
            }
            catch (Exception ex) { throw new IOException("Shell could not read the Desktop identity of " + displayName, ex); }
        }
        finally { if (item != null) Marshal.ReleaseComObject(item); }
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
