using Nepply.Models;
using Nepply.Modules.Settings;

internal static class ShortcutBindingTests
{
    private const int Esc = 0x1B, F5 = 0x74, Space = 0x20, F = 0x46, D = 0x44, R = 0x52;
    internal static void Run(Action<string, bool> check)
    {
        var settings = new AppSettings();
        check("Shortcut defaults keep the existing keys",
            ShortcutBindings.Get(settings, ShortcutAction.TogglePanel) == new ShortcutGesture(2, Space)
            && ShortcutBindings.Get(settings, ShortcutAction.FocusSearch) == new ShortcutGesture(2, F));
        check("New shortcut actions start unassigned",
            new[] { ShortcutAction.SearchDesktop, ShortcutAction.ToggleDesktopIcons, ShortcutAction.TogglePin, ShortcutAction.OpenSettings, ShortcutAction.RefreshDesktop }
                .All(a => ShortcutBindings.Get(settings, a).IsEmpty));
        check("Defaults are not persisted", settings.Shortcuts.Length == 0);
        check("Every action is either global or panel-only, never both",
            ShortcutBindings.All.Distinct().Count() == Enum.GetValues<ShortcutAction>().Length
            && ShortcutBindings.Global.Intersect(ShortcutBindings.Panel).Any() == false);

        check("Global shortcuts need Ctrl or Alt",
            !ShortcutBindings.IsValid(ShortcutAction.SearchDesktop, new(4, D)) && !ShortcutBindings.IsValid(ShortcutAction.SearchDesktop, new(0, F5))
            && ShortcutBindings.IsValid(ShortcutAction.SearchDesktop, new(3, D)) && ShortcutBindings.IsValid(ShortcutAction.OpenSettings, new(1, F5)));
        check("Esc is reserved for backing out and hiding, so no action can take it",
            ShortcutBindings.All.All(a => !ShortcutBindings.IsValid(a, new(0, Esc)) && !ShortcutBindings.IsValid(a, new(2, Esc))));
        check("Panel shortcuts allow a bare function key but not a bare letter",
            ShortcutBindings.IsValid(ShortcutAction.RefreshDesktop, new(0, F5))
            && !ShortcutBindings.IsValid(ShortcutAction.FocusSearch, new(0, F)) && !ShortcutBindings.IsValid(ShortcutAction.FocusSearch, new(4, F))
            && ShortcutBindings.IsValid(ShortcutAction.FocusSearch, new(1, F)));
        check("Only the open/close shortcut cannot be cleared",
            !ShortcutBindings.IsValid(ShortcutAction.TogglePanel, ShortcutGesture.None)
            && ShortcutBindings.All.Where(a => a != ShortcutAction.TogglePanel).All(a => ShortcutBindings.IsValid(a, ShortcutGesture.None)));

        var edited = new AppSettings();
        ShortcutBindings.Set(edited, ShortcutAction.SearchDesktop, new(3, D));
        ShortcutBindings.Set(edited, ShortcutAction.FocusSearch, ShortcutGesture.None);
        ShortcutBindings.Set(edited, ShortcutAction.RefreshDesktop, new(0, F5));
        ShortcutBindings.Set(edited, ShortcutAction.TogglePanel, new(1, Space));
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(edited))!;
        roundTrip.Validate();
        check("Rebound and cleared shortcuts survive a save round trip",
            ShortcutBindings.Get(roundTrip, ShortcutAction.SearchDesktop) == new ShortcutGesture(3, D)
            && ShortcutBindings.Get(roundTrip, ShortcutAction.FocusSearch).IsEmpty
            && ShortcutBindings.Get(roundTrip, ShortcutAction.RefreshDesktop) == new ShortcutGesture(0, F5)
            && roundTrip.HotkeyModifiers == 1 && roundTrip.HotkeyKey == Space);
        ShortcutBindings.Set(edited, ShortcutAction.FocusSearch, ShortcutBindings.Default(ShortcutAction.FocusSearch));
        check("Restoring a default removes its stored entry", !edited.Shortcuts.Any(e => e.StartsWith("FocusSearch:")));
        bool rejected = false;
        try { ShortcutBindings.Set(edited, ShortcutAction.SearchDesktop, new(0, D)); } catch (ArgumentException) { rejected = true; }
        check("Setting an invalid shortcut is rejected", rejected && ShortcutBindings.Get(edited, ShortcutAction.SearchDesktop) == new ShortcutGesture(3, D));

        check("Conflicts name the action already using a shortcut",
            ShortcutBindings.ConflictFor(edited, ShortcutAction.OpenSettings, new(3, D)) == ShortcutAction.SearchDesktop
            && ShortcutBindings.ConflictFor(edited, ShortcutAction.SearchDesktop, new(3, D)) == null
            && ShortcutBindings.ConflictFor(edited, ShortcutAction.TogglePin, new(1, Space)) == ShortcutAction.TogglePanel
            && ShortcutBindings.ConflictFor(edited, ShortcutAction.TogglePin, ShortcutGesture.None) == null);

        var damaged = new AppSettings
        {
            Shortcuts = ["Unknown:2:68", "SearchDesktop:x:1", "3:2:68", "TogglePanel:1:65", "OpenSettings:4:68",
            "TogglePin:2:32", "SearchDesktop:2:82", "RefreshDesktop:2:82", null!, "FocusSearch:0:0", "HidePanel:9:27"]
        };
        damaged.Validate();
        check("Validation drops malformed, unknown, invalid and duplicate shortcuts",
            ShortcutBindings.Get(damaged, ShortcutAction.SearchDesktop) == new ShortcutGesture(2, R)
            && ShortcutBindings.Get(damaged, ShortcutAction.RefreshDesktop).IsEmpty
            && ShortcutBindings.Get(damaged, ShortcutAction.TogglePin).IsEmpty
            && ShortcutBindings.Get(damaged, ShortcutAction.OpenSettings).IsEmpty
            && ShortcutBindings.Get(damaged, ShortcutAction.FocusSearch).IsEmpty
            && !damaged.Shortcuts.Any(e => e.StartsWith("HidePanel:"))
            && damaged.HotkeyModifiers == 2 && damaged.HotkeyKey == Space);
        var legacyFunction = new AppSettings { HotkeyModifiers = 2, HotkeyKey = F5 };
        legacyFunction.Validate();
        check("Open/close accepts Ctrl or Alt with a function key", legacyFunction.HotkeyKey == F5);

        var original = new AppSettings();
        var clone = original.Clone();
        ShortcutBindings.Set(clone, ShortcutAction.TogglePin, new(2, 0x50));
        check("Editing a cloned draft leaves the original shortcuts untouched", ShortcutBindings.Get(original, ShortcutAction.TogglePin).IsEmpty);
        check("Shortcut changes request hotkey refresh only", SettingsChangeSet.Between(original, clone).Changes == SettingsChange.Hotkey);
        check("Shortcut labels format Esc and function keys",
            new ShortcutGesture(0, Esc).ToString() == "Esc" && new ShortcutGesture(3, F5).ToString() == "Ctrl + Alt + F5" && ShortcutGesture.None.ToString() == "");
    }
}
