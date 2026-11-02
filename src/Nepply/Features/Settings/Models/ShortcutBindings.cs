using Nepply.Helpers;

namespace Nepply.Models;

/// <summary>Every rebindable action. Global actions work from any app; panel actions only while Nepply has focus.</summary>
public enum ShortcutAction { TogglePanel, SearchDesktop, ToggleDesktopIcons, TogglePin, OpenSettings, FocusSearch, RefreshDesktop }

/// <summary>Win32 modifier bits (Alt=1, Ctrl=2, Shift=4) plus a virtual key. Key 0 means unassigned.</summary>
public readonly record struct ShortcutGesture(int Modifiers, int Key)
{
    public const int Alt = 1, Control = 2, Shift = 4;
    public static readonly ShortcutGesture None = new(0, 0);
    public bool IsEmpty => Key == 0;
    public override string ToString() => IsEmpty ? "" : HotkeyGesture.Format(Modifiers, Key);
}

/// <summary>
/// Shortcut rules shared by settings validation, the recorder and the dispatchers.
/// TogglePanel keeps its original HotkeyModifiers/HotkeyKey storage; every other
/// action is stored in AppSettings.Shortcuts as "Action:modifiers:key". A missing
/// entry means the default; "Action:0:0" means deliberately unassigned.
/// </summary>
public static class ShortcutBindings
{
    public static readonly ShortcutAction[] Global =
        [ShortcutAction.TogglePanel, ShortcutAction.SearchDesktop, ShortcutAction.ToggleDesktopIcons, ShortcutAction.TogglePin, ShortcutAction.OpenSettings];
    public static readonly ShortcutAction[] Panel =
        [ShortcutAction.FocusSearch, ShortcutAction.RefreshDesktop];
    public static IEnumerable<ShortcutAction> All => Global.Concat(Panel);

    private const int Escape = 0x1B, F1 = 0x70, F12 = 0x7B;
    internal static bool IsFunctionKey(int key) => key is >= F1 and <= F12;

    public static bool IsGlobal(ShortcutAction action) => Array.IndexOf(Global, action) >= 0;

    /// <summary>Only what people expect to work at once has a default; the rest starts unassigned.</summary>
    public static ShortcutGesture Default(ShortcutAction action) => action switch
    {
        ShortcutAction.TogglePanel => new(ShortcutGesture.Alt | ShortcutGesture.Shift, 0x20),
        ShortcutAction.FocusSearch => new(ShortcutGesture.Control, 0x46),
        _ => ShortcutGesture.None
    };

    public static bool CanClear(ShortcutAction action) => action != ShortcutAction.TogglePanel;

    /// <summary>
    /// Global gestures need Ctrl or Alt so they cannot swallow normal typing in other apps.
    /// Panel gestures may be a bare function key; letters, digits and other keys still need
    /// Ctrl or Alt because the panel has a search box. Esc is reserved: it backs out of the
    /// current interaction and then hides the panel (show/hide already covers hiding).
    /// </summary>
    public static bool IsValid(ShortcutAction action, ShortcutGesture gesture)
    {
        if (gesture.IsEmpty) return CanClear(action) && gesture.Modifiers == 0;
        if (gesture.Modifiers is < 0 or > 7 || gesture.Key == Escape) return false;
        bool commandModifier = (gesture.Modifiers & (ShortcutGesture.Alt | ShortcutGesture.Control)) != 0;
        bool standardKey = HotkeyGesture.IsSupportedKey(gesture.Key) || IsFunctionKey(gesture.Key);
        if (IsGlobal(action)) return commandModifier && standardKey;
        if (IsFunctionKey(gesture.Key)) return true;
        return commandModifier && HotkeyGesture.IsSupportedKey(gesture.Key);
    }

    public static ShortcutGesture Get(AppSettings settings, ShortcutAction action)
    {
        if (action == ShortcutAction.TogglePanel) return new(settings.HotkeyModifiers, settings.HotkeyKey);
        foreach (var entry in settings.Shortcuts ?? [])
            if (TryParse(entry, out var stored, out var gesture) && stored == action) return gesture;
        return Default(action);
    }

    public static void Set(AppSettings settings, ShortcutAction action, ShortcutGesture gesture)
    {
        if (!IsValid(action, gesture)) throw new ArgumentException("Unsupported shortcut for this action.", nameof(gesture));
        if (action == ShortcutAction.TogglePanel) { settings.HotkeyModifiers = gesture.Modifiers; settings.HotkeyKey = gesture.Key; return; }
        // Replace the array instead of mutating it: settings clones share arrays until changed.
        var others = (settings.Shortcuts ?? []).Where(e => !TryParse(e, out var stored, out _) || stored != action);
        settings.Shortcuts = gesture == Default(action) ? others.ToArray() : others.Append(Format(action, gesture)).ToArray();
    }

    /// <summary>The other action already using this gesture, if any.</summary>
    public static ShortcutAction? ConflictFor(AppSettings settings, ShortcutAction action, ShortcutGesture gesture)
    {
        if (gesture.IsEmpty) return null;
        foreach (var other in All)
            if (other != action && Get(settings, other) == gesture) return other;
        return null;
    }

    /// <summary>Drops unknown, malformed or invalid entries; a duplicated gesture keeps the earlier action.</summary>
    internal static string[] Normalize(AppSettings settings)
    {
        var parsed = new Dictionary<ShortcutAction, ShortcutGesture>();
        foreach (var entry in (settings.Shortcuts ?? []).Take(64))
            if (TryParse(entry, out var action, out var gesture) && action != ShortcutAction.TogglePanel && IsValid(action, gesture))
                parsed[action] = gesture;
        var used = new HashSet<ShortcutGesture> { new(settings.HotkeyModifiers, settings.HotkeyKey) };
        var result = new List<string>();
        foreach (var action in All.Where(a => a != ShortcutAction.TogglePanel))
        {
            var gesture = parsed.TryGetValue(action, out var stored) ? stored : Default(action);
            if (!gesture.IsEmpty && !used.Add(gesture)) gesture = ShortcutGesture.None;
            if (gesture != Default(action)) result.Add(Format(action, gesture));
        }
        return result.ToArray();
    }

    internal static string Format(ShortcutAction action, ShortcutGesture gesture) => $"{action}:{gesture.Modifiers}:{gesture.Key}";

    internal static bool TryParse(string? entry, out ShortcutAction action, out ShortcutGesture gesture)
    {
        action = default; gesture = ShortcutGesture.None;
        var parts = entry?.Split(':');
        if (parts is not { Length: 3 } || parts[0].Length == 0 || !char.IsAsciiLetter(parts[0][0]) || !Enum.TryParse(parts[0], false, out action) || !Enum.IsDefined(action)
            || !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int modifiers)
            || !int.TryParse(parts[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int key)) return false;
        gesture = new(modifiers, key);
        return true;
    }
}
