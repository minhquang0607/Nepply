using System.ComponentModel;

namespace Nepply.Services;

internal static class HelperFailure
{
    internal const string PolicyMessage = "Windows Application Control đã chặn thành phần Nepply (4551). Liên hệ nhà phát hành hoặc quản trị viên; không tắt bảo vệ Windows.";
    internal static bool IsPolicyBlocked(Exception error) => error is Win32Exception { NativeErrorCode: 4551 };
}
