using System.ComponentModel;

namespace Nepply.Services;

internal static class HelperFailure
{
    internal const string PolicyMessageKey = "PolicyBlocked";
    internal static bool IsPolicyBlocked(Exception error) => error is Win32Exception { NativeErrorCode: 4551 };
}
