namespace Nepply.Modules.Desktop;

/// <summary>One immutable configuration revision, captured once per scan.</summary>
internal sealed record DesktopScanOptions(bool ShowHiddenFiles = false,
    bool ShowSystemIcons = true, bool MergeEquivalentShortcuts = true);
