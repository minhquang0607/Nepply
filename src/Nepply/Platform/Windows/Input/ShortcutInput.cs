using System.Windows.Input;
using Nepply.Models;

namespace Nepply.Helpers;

/// <summary>Converts WPF key events into stored shortcut gestures.</summary>
internal static class ShortcutInput
{
    /// <summary>The gesture for a key press, or None for modifier-only or Windows-key presses.</summary>
    internal static ShortcutGesture FromKey(Key key, Key systemKey, ModifierKeys modifiers)
    {
        var actual = key == Key.System ? systemKey : key;
        if (actual is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin || (modifiers & ModifierKeys.Windows) != 0) return ShortcutGesture.None;
        int bits = ((modifiers & ModifierKeys.Alt) != 0 ? ShortcutGesture.Alt : 0)
            | ((modifiers & ModifierKeys.Control) != 0 ? ShortcutGesture.Control : 0)
            | ((modifiers & ModifierKeys.Shift) != 0 ? ShortcutGesture.Shift : 0);
        return new(bits, KeyInterop.VirtualKeyFromKey(actual));
    }

    internal static ShortcutGesture FromEvent(KeyEventArgs e) => FromKey(e.Key, e.SystemKey, Keyboard.Modifiers);

    internal static bool Matches(AppSettings settings, ShortcutAction action, ShortcutGesture pressed)
        => !pressed.IsEmpty && ShortcutBindings.Get(settings, action) == pressed;

    /// <summary>"Name · Shortcut" when the action has a key, otherwise just the name.</summary>
    internal static string Label(string name, AppSettings settings, ShortcutAction action)
    {
        var gesture = ShortcutBindings.Get(settings, action);
        return gesture.IsEmpty ? name : name + " · " + gesture;
    }
}
