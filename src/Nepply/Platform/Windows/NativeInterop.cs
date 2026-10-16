using System;
using System.Runtime.InteropServices;

namespace Nepply.Helpers;

internal static class NativeInterop
{
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SHGetFileInfoPidl(IntPtr pidl, uint attributes, ref SHFILEINFO info, uint size, uint flags);

    [DllImport("user32.dll")]
    internal static extern bool DestroyIcon(IntPtr handle);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHParseDisplayName(string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    [DllImport("shell32.dll")]
    internal static extern int SHBindToParent(IntPtr pidl, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv, out IntPtr ppidlLast);

    [DllImport("user32.dll")]
    internal static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    internal static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    internal static extern int TrackPopupMenuEx(IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CMINVOKECOMMANDINFOEX
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? lpParameters;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? lpTitle;
        public IntPtr lpVerbW;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpParametersW;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpDirectoryW;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpTitleW;
        public POINT ptInvoke;
    }

    internal const uint SHGFI_ICON = 0x000000100;
    internal const uint SHGFI_LARGEICON = 0x000000000;
    internal const uint WM_COMMAND = 0x0111;

    internal static readonly Guid IID_IShellFolder = new("000214E6-0000-0000-C000-000000000046");
    internal static readonly Guid IID_IContextMenu = new("000214e4-0000-0000-c000-000000000046");

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    internal interface IShellFolder
    {
        void ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, ref uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
        void EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr enumIDList);
        void BindToObject(IntPtr pidl, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void BindToStorage(IntPtr pidl, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        [PreserveSig] int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        void CreateViewObject(IntPtr hwndOwner, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref uint rgfInOut);
        void GetUIObjectOf(IntPtr hwndOwner, uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, IntPtr rgfReserved, out IntPtr ppv);
        void GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);
        void SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214e4-0000-0000-c000-000000000046")]
    internal interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        void InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        void GetCommandString(uint idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
    }

    // Repeat inherited vtable slots explicitly for COM interop.
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214f4-0000-0000-c000-000000000046")]
    internal interface IContextMenu2
    {
        [PreserveSig] int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags);
        void InvokeCommand(ref CMINVOKECOMMANDINFOEX command);
        void GetCommandString(UIntPtr command, uint type, IntPtr reserved, IntPtr name, uint length);
        [PreserveSig] int HandleMenuMsg(uint message, IntPtr wParam, IntPtr lParam);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719")]
    internal interface IContextMenu3
    {
        [PreserveSig] int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags);
        void InvokeCommand(ref CMINVOKECOMMANDINFOEX command);
        void GetCommandString(UIntPtr command, uint type, IntPtr reserved, IntPtr name, uint length);
        [PreserveSig] int HandleMenuMsg(uint message, IntPtr wParam, IntPtr lParam);
        [PreserveSig] int HandleMenuMsg2(uint message, IntPtr wParam, IntPtr lParam, out IntPtr result);
    }

    // --- IImageList / SHGetImageList Definitions ---

    internal const int SHIL_LARGE = 0;
    internal const int SHIL_SMALL = 1;
    internal const int SHIL_EXTRALARGE = 2; // 48x48
    internal const int SHIL_SYSSMALL = 3;
    internal const int SHIL_JUMBO = 4;      // 256x256

    internal const uint SHGFI_SYSICONINDEX = 0x000004000;
    internal const int ILD_TRANSPARENT = 0x00000001;

    [DllImport("shell32.dll", EntryPoint = "SHGetImageList", ExactSpelling = true)]
    internal static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

    internal static readonly Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
        [PreserveSig] int Draw(IntPtr pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, out IntPtr picon);
        // Additional methods omitted as they are not used.
    }
}
