using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using static Nepply.Helpers.NativeInterop;

namespace Nepply.Services;

internal static class AutoStartService
{
    internal const string TurnedOffNotice = "AutoStartOffInWindows";
    private const string StartupApprovedKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    internal static UserMessage? Apply(bool enabled)
    {
        // HKCU Run and the Startup shortcut belong only to unpackaged installs.
        // The MSIX adapter must use StartupTask; never persist a versioned WindowsApps path.
        EnsureUnpackaged(ProcessPackageIdentity.IsPackaged);
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Application path is unavailable.");
        using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
        var existing = key.GetValue("Nepply", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (existing != null && !MatchesRunCommand(existing, executable))
        {
            if (!IsDeadRunCommand(existing, ProductIdentity.Name + ".exe")) return UserMessage.FromKey("AutoStartDifferentEntry");
            AppLogger.Info("Replacing startup entry of a moved or deleted Nepply copy.");
        }
        if (enabled && existing == null) ForgetOrphanedState();
        if (enabled) key.SetValue("Nepply", "\"" + executable + "\"", RegistryValueKind.String);
        else key.DeleteValue("Nepply", false);
        return RetireShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ProductIdentity.Name + ".lnk"), executable);
    }

    // Task Manager and Windows Settings keep their own switch here. It is the user's
    // choice, so report it instead of changing it.
    internal static bool IsTurnedOffInWindows()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(StartupApprovedKey);
        return IsStartupStateOff(approved?.GetValue("Nepply"));
    }

    // Windows keeps that switch by entry name and never removes it, so one left behind
    // by a removed entry would silently turn off the new entry. Only a live entry has a
    // switch worth respecting; never call this while one exists.
    internal static void ForgetOrphanedState(string keyPath = StartupApprovedKey)
    {
        try
        {
            using var approved = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
            approved?.DeleteValue("Nepply", false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            AppLogger.Error(ex, "Clear orphaned Windows startup state");
        }
    }

    internal static bool IsStartupStateOff(object? state) => state is byte[] { Length: > 0 } bytes && (bytes[0] & 1) != 0;

    internal static void EnsureUnpackaged(bool isPackaged)
    {
        if (isPackaged)
            throw new InvalidOperationException("Packaged startup requires StartupTask, not HKCU Run.");
    }

    private static UserMessage? RetireShortcut(string shortcut, string executable)
    {
        if (!File.Exists(shortcut)) return null;
        object? instance = null;
        try
        {
            instance = new ShellLink();
            var link = (IShellLinkW)instance;
            ((System.Runtime.InteropServices.ComTypes.IPersistFile)link).Load(shortcut, 0);
            var target = new StringBuilder(32768);
            var arguments = new StringBuilder(32768);
            link.GetPath(target, target.Capacity, IntPtr.Zero, 0);
            link.GetArguments(arguments, arguments.Capacity);
            // A matching filename does not establish ownership of a Startup shortcut.
            if (!MatchesCurrentApplication(target.ToString(), arguments.ToString(), executable))
                return UserMessage.FromKey("AutoStartSeparateShortcut");
        }
        finally
        {
            if (instance != null && Marshal.IsComObject(instance)) Marshal.ReleaseComObject(instance);
        }
        string backup = shortcut + ".disabled";
        if (File.Exists(backup)) backup = shortcut + ".disabled-" + Guid.NewGuid().ToString("N");
        File.Move(shortcut, backup); // Recoverable; never overwrite an earlier backup.
        return null;
    }

    internal static bool MatchesRunCommand(object? command, string executable) =>
        command is string text && Path.IsPathFullyQualified(executable)
        && string.Equals(text, "\"" + executable + "\"", StringComparison.OrdinalIgnoreCase);

    internal static bool IsDeadRunCommand(object? command, string fileName) =>
        IsDeadRunCommand(command, fileName, File.Exists, IsPresentFixedRoot);

    // Stale entry of a moved/deleted copy: exactly the format Apply writes, the expected
    // file name, and a missing file on a fixed local drive that is present right now.
    // Removable, network and absent drives may hold a live copy that is merely offline.
    internal static bool IsDeadRunCommand(object? command, string fileName, Func<string, bool> fileExists, Func<string, bool> isPresentFixedRoot)
    {
        if (command is not string text || text.Length < 3 || text[0] != '"' || text[^1] != '"') return false;
        string path = text[1..^1];
        if (path.Contains('"') || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal)) return false;
        if (!string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase)) return false;
        string? root = Path.GetPathRoot(path);
        return !string.IsNullOrEmpty(root) && isPresentFixedRoot(root) && !fileExists(path);
    }

    private static bool IsPresentFixedRoot(string root)
    {
        try { var drive = new DriveInfo(root); return drive.DriveType == DriveType.Fixed && drive.IsReady; }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { return false; }
    }

    internal static bool MatchesCurrentApplication(string target, string arguments, string executable)
    {
        if (!string.IsNullOrWhiteSpace(arguments) || !Path.IsPathFullyQualified(target)
            || !Path.IsPathFullyQualified(executable)) return false;
        try { return string.Equals(Path.GetFullPath(target), Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }
}
