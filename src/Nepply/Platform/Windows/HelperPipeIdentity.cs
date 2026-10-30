using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Nepply.Helpers;

internal static class HelperPipeIdentity
{
    internal static void RequireServer(NamedPipeClientStream pipe, System.Diagnostics.Process parent)
    {
        // Cache a real process handle before querying the PID; it pins that process identity.
        _ = parent.SafeHandle;
        if (parent.HasExited) throw new UnauthorizedAccessException("Helper parent has exited.");
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint actualProcessId))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot verify helper pipe server.");
        if (actualProcessId != (uint)parent.Id || parent.HasExited)
            throw new UnauthorizedAccessException("Unexpected helper pipe server.");
    }
    // CurrentUserOnly remains necessary. PID binding additionally rejects an
    // unrelated process under the same account before exchanging any payload.
    internal static void RequireClient(NamedPipeServerStream pipe, int expectedProcessId)
    {
        if (expectedProcessId <= 0) throw new ArgumentOutOfRangeException(nameof(expectedProcessId));
        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint actualProcessId))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot verify helper pipe client.");
        if (actualProcessId != (uint)expectedProcessId)
            throw new UnauthorizedAccessException("Unexpected helper pipe client.");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);
}
