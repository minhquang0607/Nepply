using Microsoft.Win32;
using Nepply.Models;

namespace Nepply.Services;

/// <summary>What the person picked in Setup, handed over once through HKCU.</summary>
internal static class InstallerChoices
{
    internal const string KeyPath = @"Software\Nepply\Setup";

    internal static void Apply(AppSettings settings, Action save, string keyPath = KeyPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        if (key == null) return;
        bool changed = false;
        // Once someone has chosen a language in Nepply, reinstalling must not replace it.
        if (!settings.HasCompletedOnboarding && key.GetValue("Language") is string language && LocalizationService.IsSupported(language))
        {
            settings.Language = language;
            changed = true;
        }
        // Settings kept from an earlier install must not overrule the box just unticked.
        if (key.GetValue("AutoStart") is int autoStart && (autoStart != 0) != settings.AutoStart)
        {
            settings.AutoStart = autoStart != 0;
            changed = true;
        }
        if (changed) save();
        key.DeleteValue("Language", false);
        key.DeleteValue("AutoStart", false);
    }
}
