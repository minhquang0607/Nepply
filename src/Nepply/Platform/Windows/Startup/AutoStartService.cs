using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using static Nepply.Helpers.NativeInterop;

namespace Nepply.Services;

internal static class AutoStartService
{
    internal static UserMessage? Apply(bool enabled)
    {
        // HKCU Run/Startup shortcut migration belongs only to unpackaged installs.
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
        string previousExecutable = Path.Combine(Path.GetDirectoryName(executable)!, ProductIdentity.LegacyName + ".exe");
        var previous = key.GetValue(ProductIdentity.LegacyName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (enabled) key.SetValue("Nepply", "\"" + executable + "\"", RegistryValueKind.String);
        else key.DeleteValue("Nepply", false);
        // Only migrate an exact, argument-free entry in this installation directory, or
        // retire one whose executable is gone. Live portable installations and
        // customized commands belong to the user.
        if (MatchesRunCommand(previous, previousExecutable) || IsDeadRunCommand(previous, ProductIdentity.LegacyName + ".exe"))
            key.DeleteValue(ProductIdentity.LegacyName, false);
        else if (previous != null)
            return UserMessage.FromKey("AutoStartLegacyEntry");

        foreach (var name in new[] { ProductIdentity.Name, ProductIdentity.LegacyName })
        {
            string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), name + ".lnk");
            var notice = RetireShortcut(shortcut, executable, name == ProductIdentity.LegacyName ? previousExecutable : executable);
            if (notice != null) return notice;
        }
        return null;
    }

    internal static void EnsureUnpackaged(bool isPackaged)
    {
        if (isPackaged)
            throw new InvalidOperationException("Packaged startup requires StartupTask, not HKCU Run.");
    }

    private static UserMessage? RetireShortcut(string legacy, string executable, string previousExecutable)
    {
        if (!File.Exists(legacy)) return null;
        object? instance = null;
        try
        {
            instance = new ShellLink();
            var link = (IShellLinkW)instance;
            ((System.Runtime.InteropServices.ComTypes.IPersistFile)link).Load(legacy, 0);
            var target = new StringBuilder(32768);
            var arguments = new StringBuilder(32768);
            link.GetPath(target, target.Capacity, IntPtr.Zero, 0);
            link.GetArguments(arguments, arguments.Capacity);
            // A matching filename does not establish ownership of a Startup shortcut.
            if (!MatchesCurrentApplication(target.ToString(), arguments.ToString(), executable)
                && !MatchesCurrentApplication(target.ToString(), arguments.ToString(), previousExecutable))
                return UserMessage.FromKey("AutoStartSeparateShortcut");
        }
        finally
        {
            if (instance != null && Marshal.IsComObject(instance)) Marshal.ReleaseComObject(instance);
        }
        string backup = legacy + ".disabled";
        if (File.Exists(backup)) backup = legacy + ".disabled-" + Guid.NewGuid().ToString("N");
        File.Move(legacy, backup); // Recoverable; never overwrite an earlier backup.
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
