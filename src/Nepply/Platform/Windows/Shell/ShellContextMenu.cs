using System.Runtime.InteropServices;
using Nepply.Services;
using DesktopIconModel = Nepply.Models.DesktopIcon;
using static Nepply.Helpers.NativeInterop;

namespace Nepply.Helpers;

/// <summary>Owns the Shell menu, COM references and PIDL for one synchronous invocation.</summary>
internal static class ShellContextMenu
{
    internal static UserMessage? Show(DesktopIconModel icon, IntPtr hwnd, System.Windows.Point? screenAnchor = null)
    {
        try
        {
            var path = icon.Path;

            // The menu belongs to the desktop entry, never the shortcut's target.

            // Parse the file path into a PIDL
            int hr = SHParseDisplayName(path, IntPtr.Zero, out IntPtr pidl, 0, out _);
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            if (pidl == IntPtr.Zero) throw new InvalidOperationException("Shell returned an empty item identity.");

            try
            {
                // Get parent folder and child PIDL
                hr = SHBindToParent(pidl, IID_IShellFolder, out IntPtr psfParent, out IntPtr pidlChild);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                if (psfParent == IntPtr.Zero) throw new InvalidOperationException("Shell returned an empty parent folder.");

                try
                {
                    // Get IContextMenu from the parent folder
                    var shellFolder = (IShellFolder)Marshal.GetObjectForIUnknown(psfParent);
                    try
                    {
                        IntPtr[] children = { pidlChild };

                        shellFolder.GetUIObjectOf(hwnd, (uint)children.Length, children,
                            IID_IContextMenu, IntPtr.Zero, out IntPtr pContextMenu);

                        if (pContextMenu == IntPtr.Zero) throw new InvalidOperationException("Shell returned no context menu.");

                        try
                        {
                            var contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(pContextMenu);
                            try
                            {
                                IntPtr hMenu = CreatePopupMenu();
                                if (hMenu == IntPtr.Zero) throw new InvalidOperationException("Windows could not create a popup menu.");

                                try
                                {
                                    using var menuSession = new ShellMenuSession(hwnd, contextMenu);
                                    // Populate the menu
                                    int menuResult = contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, 0); // CMF_NORMAL
                                    if (menuResult < 0) Marshal.ThrowExceptionForHR(menuResult);

                                    // Show the menu
                                    POINT pt;
                                    if (screenAnchor is { } anchor && double.IsFinite(anchor.X) && double.IsFinite(anchor.Y))
                                        pt = new POINT { X = (int)Math.Round(anchor.X), Y = (int)Math.Round(anchor.Y) };
                                    else if (!GetCursorPos(out pt)) throw new InvalidOperationException("Windows could not locate the pointer.");
                                    const uint TPM_LEFTALIGN = 0x0000;
                                    const uint TPM_RETURNCMD = 0x0100;
                                    int cmd = PopupMenuTracking.Track(hwnd,
                                        () => TrackPopupMenuEx(hMenu, TPM_LEFTALIGN | TPM_RETURNCMD, pt.X, pt.Y, hwnd, IntPtr.Zero));

                                    if (cmd > 0)
                                    {
                                        // Execute the selected command
                                        var cmi = new CMINVOKECOMMANDINFOEX
                                        {
                                            cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                                            fMask = 0,
                                            hwnd = hwnd,
                                            lpVerb = (IntPtr)(cmd - 1),
                                            lpVerbW = (IntPtr)(cmd - 1),
                                            nShow = 1 // SW_SHOWNORMAL
                                        };
                                        contextMenu.InvokeCommand(ref cmi);
                                    }
                                }
                                finally
                                {
                                    DestroyMenu(hMenu);
                                }
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(contextMenu);
                            }
                        }
                        finally
                        {
                            Marshal.Release(pContextMenu);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(shellFolder);
                    }
                }
                finally
                {
                    Marshal.Release(psfParent);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Show Shell context menu");
            return UserMessage.FromKey("DesktopMenuError", icon.Name);
        }
        return null;
    }
}
