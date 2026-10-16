using System.Runtime.InteropServices;
using Nepply.Helpers;

namespace Nepply.Services;

internal readonly record struct PointerContext(int X, int Y, nint Foreground, uint ProcessId, uint CursorFlags)
{
    internal bool CursorVisible => (CursorFlags & 1) != 0 && (CursorFlags & 2) == 0;
}

internal static class PointerContextReader
{
    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        internal uint Size, Flags;
        internal nint Cursor; // Borrowed system handle: never destroy it.
        internal NativeInterop.POINT Position;
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorInfo(ref CursorInfo info);

    internal static PointerContext? Read()
    {
        var foreground = GetForegroundWindow();
        var info = new CursorInfo { Size = (uint)Marshal.SizeOf<CursorInfo>() };
        if (foreground == 0 || !GetCursorInfo(ref info) ||
            GetWindowThreadProcessId(foreground, out var processId) == 0 || processId == 0 ||
            GetForegroundWindow() != foreground) return null;
        return new(info.Position.X, info.Position.Y, foreground, processId, info.Flags);
    }
}
