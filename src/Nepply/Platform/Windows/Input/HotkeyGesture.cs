using System.Windows.Input;

namespace Nepply.Helpers;

internal static class HotkeyGesture
{
    // Keep the persisted VK contract; virtual-key numbers are not character codes.
    internal static bool IsSupportedKey(int key) => key is >= 32 and <= 90;
    internal static string Format(int modifiers, int virtualKey)
    {
        string key = virtualKey switch
        {
            0x1B => "Esc",
            33 => "PageUp",
            34 => "PageDown",
            _ => virtualKey is >= 48 and <= 57 or >= 65 and <= 90
            ? ((char)virtualKey).ToString()
            : KeyInterop.KeyFromVirtualKey(virtualKey) is { } mapped && mapped != Key.None
                ? mapped.ToString() : $"0x{virtualKey:X}"
        };
        return ((modifiers & 2) != 0 ? "Ctrl + " : "")
            + ((modifiers & 1) != 0 ? "Alt + " : "")
            + ((modifiers & 4) != 0 ? "Shift + " : "") + key;
    }
}
