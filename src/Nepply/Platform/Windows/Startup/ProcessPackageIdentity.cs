using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Nepply.Services;

internal static class ProcessPackageIdentity
{
    // Process identity cannot change during this process lifetime. An API failure
    // must remain a failure, never permission to write unpackaged startup entries.
    private static readonly Lazy<bool> Packaged = new(Read);
    internal static bool IsPackaged => Packaged.Value;
    // Explorer's toggle is undocumented, which Store policy 10.2.8 rules out, and a
    // package has no uninstall step to bring hidden icons back.
    internal static bool CanHideDesktopIcons => !IsPackaged;

    private static bool Read()
    {
        uint length = 0;
        int result = GetCurrentPackageFullName(ref length, IntPtr.Zero);
        return Classify(result, length);
    }

    internal static bool Classify(int result, uint requiredLength) => result switch
    {
        15700 => false, // APPMODEL_ERROR_NO_PACKAGE
        122 when requiredLength > 1 => true, // ERROR_INSUFFICIENT_BUFFER: identity exists
        122 or 0 => throw new InvalidOperationException("Invalid package identity size query result."),
        _ => throw new Win32Exception(result)
    };

    // This API returns the error code directly; GetLastWin32Error is not its result.
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, IntPtr packageFullName);
}
