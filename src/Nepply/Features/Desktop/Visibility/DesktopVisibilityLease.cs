using System.IO;

namespace Nepply.Services;

/// <summary>Journal only changes made by Nepply; a pre-hidden Desktop is never claimed.</summary>
public sealed class DesktopVisibilityLease(IDesktopVisibility desktop, string journal)
{
    // A packaged build still restores what an installed build hid; never duplicate an ownership lease.
    private static string UnpackagedJournalPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductIdentity.Name, "desktop-visibility.lease");
    public static string JournalPath => ProcessPackageIdentity.IsPackaged
        ? Path.Combine(Windows.Storage.ApplicationData.Current.LocalCacheFolder.Path, ProductIdentity.Name, "desktop-visibility.lease")
        : UnpackagedJournalPath;
    public static DesktopVisibilityLease Create() =>
        new(ProcessPackageIdentity.IsPackaged ? new WindowDesktopVisibility() : new ExplorerDesktopVisibility(), JournalPath);
    internal static bool RestorePreviousSession()
    {
        using var fence = DesktopGuardianFence.Acquire(TimeSpan.FromSeconds(15));
        if (!string.Equals(UnpackagedJournalPath, JournalPath, StringComparison.OrdinalIgnoreCase)
            && !new DesktopVisibilityLease(new ExplorerDesktopVisibility(), UnpackagedJournalPath).Restore()) return false;
        return Create().Restore();
    }
    public bool OwnsChange => File.Exists(journal);
    internal bool IsCurrentlyHidden => desktop.IsVisible() == false;
    internal bool IsCurrentlyVisible => desktop.IsVisible() == true;
    public bool Hide()
    {
        if (desktop.IsVisible() is not true) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(journal))!);
        // Durable intent is written BEFORE touching Explorer, including the crash-between-steps case.
        using (var file = new FileStream(journal, FileMode.Create, FileAccess.Write, FileShare.Read))
        { file.Write("restore-visible-v1"u8); file.Flush(true); }
        return desktop.SetVisible(false);
    }
    public bool Restore()
    {
        if (!OwnsChange) return true;
        try { if (!HasRestoreMarker(journal)) return false; }
        // The authenticated guardian can finish recovery and remove the marker
        // between OwnsChange and opening it. Still verify visibility; absence
        // alone must not turn an incomplete restoration into success.
        catch (FileNotFoundException) { return desktop.IsVisible() == true; }
        catch (DirectoryNotFoundException) { return desktop.IsVisible() == true; }
        if (desktop.IsVisible() is not { } visible) return false;
        if (!visible && !desktop.SetVisible(true)) return false;
        File.Delete(journal);
        return true;
    }
    internal static bool HasRestoreMarker(string path) => DesktopRecoveryJournal.HasRestoreMarker(path);
}
