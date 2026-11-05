using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Nepply.Models;
using static Nepply.Helpers.NativeInterop;
namespace Nepply.Services;

/// <summary>Shell namespace includes redirected, public and virtual Desktop entries.</summary>
public static class ShellDesktopReader
{
    public static Task<T> RunSta<T>(Func<T> action)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { result.SetResult(action()); } catch (Exception ex) { result.SetException(ex); } }) { IsBackground = true, Name = "Desktop Shell" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return result.Task;
    }
    public static IReadOnlyList<DesktopIcon> Read(bool hidden, bool system, params string[] roots) =>
        ReadFrom(new(roots, Modules.Desktop.NativeDesktopNamespace.Read, File.GetAttributes), hidden, system, new());
    internal static IReadOnlyList<DesktopIcon> ReadFrom(DesktopReadSource source, bool hidden, bool system, DesktopScanIssues issues)
    {
        IReadOnlyList<Modules.Desktop.NativeDesktopNamespace.Entry> entries;
        try { entries = source.Namespace(hidden, issues); }
        catch (Exception ex) { throw new IOException("Shell Desktop namespace is unavailable or incomplete.", ex); }
        var result = new List<DesktopIcon>();
        foreach (var item in entries)
        {
            try
            {
                string path = item.Path, name = item.Name;
                if (string.IsNullOrWhiteSpace(path)) continue;
                bool physical = File.Exists(path) || Directory.Exists(path);
                bool desktopFile = physical && source.Roots.Any(r => string.Equals(Path.GetDirectoryName(path), r, StringComparison.OrdinalIgnoreCase));
                if (!desktopFile && !(system && IsDesktopSystemIcon(path))) continue;
                if (physical && !hidden && (source.Attributes(path) & FileAttributes.Hidden) != 0) continue;
                if (Path.GetFileName(path).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(new() { Name = name, Path = path, IsSystemIcon = !desktopFile });
            }
            catch (Exception ex) { issues.Skip(item.Path, ex); }
        }
        return result;
    }
    public static string ShortcutIdentity(string name, string target, string arguments, string directory) =>
        "link:" + name.ToUpperInvariant() + "\0" + target.ToUpperInvariant() + "\0" + arguments + "\0" + directory.ToUpperInvariant();
    public static bool IsDesktopSystemIcon(string path)
    {
        // Shell exposes the enabled User Files icon as the profile path on some
        // Windows versions. This is not permission to include its child folders.
        if (string.Equals(path.TrimEnd('\\'), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            path = "::{59031A47-3F72-44A7-89C5-5595FE6B30EE}";
        string id = path.ToUpperInvariant() switch
        {
            "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}" => "{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
            "::{645FF040-5081-101B-9F08-00AA002F954E}" => "{645FF040-5081-101B-9F08-00AA002F954E}",
            "::{26EE0668-A00A-44D7-9371-BEB064C98683}" or "::{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}" => "{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}",
            "::{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}" => "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}",
            "::{59031A47-3F72-44A7-89C5-5595FE6B30EE}" => "{59031A47-3F72-44A7-89C5-5595FE6B30EE}",
            _ => ""
        };
        if (id.Length == 0) return false;
        const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
        using var user = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath);
        using var machine = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
        var value = user?.GetValue(id) ?? machine?.GetValue(id);
        return value is int hidden ? hidden == 0 : id == "{645FF040-5081-101B-9F08-00AA002F954E}";
    }
    public static string Identity(DesktopIcon item)
    {
        // Both Control Panel presentations refer to the same system destination.
        if (item.Path.StartsWith("::{26EE0668-", StringComparison.OrdinalIgnoreCase) || item.Path.StartsWith("::{5399E694-", StringComparison.OrdinalIgnoreCase)) return "shell:controlpanel";
        return "path:" + item.Path.TrimEnd('\\').ToUpperInvariant();
    }
    public static string DestinationIdentity(DesktopIcon item)
    {
        if (Path.GetExtension(item.Path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            object? instance = null;
            try
            {
                instance = new ShellLink(); var link = (IShellLinkW)instance;
                ((System.Runtime.InteropServices.ComTypes.IPersistFile)link).Load(item.Path, 0);
                var target = new StringBuilder(32768); var args = new StringBuilder(32768); var dir = new StringBuilder(32768);
                link.GetPath(target, target.Capacity, IntPtr.Zero, 0); link.GetArguments(args, args.Capacity); link.GetWorkingDirectory(dir, dir.Capacity);
                // Unresolved/MSI/UWP links retain their original identity; never merge by display name alone.
                if (target.Length > 0)
                {
                    var iconPath = new StringBuilder(32768);
                    link.GetIconLocation(iconPath, iconPath.Capacity, out var iconIndex);
                    link.GetShowCmd(out var show); link.GetHotkey(out var hotkey);
                    using var file = File.OpenRead(item.Path);
                    Span<byte> header = stackalloc byte[24]; file.ReadExactly(header);
                    uint flags = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header[20..]);
                    // Run-as-admin, window mode, hotkey and custom icon change behavior.
                    // Keep those shortcuts separate even when target/arguments match.
                    return ShortcutIdentity(item.Name, target.ToString(), args.ToString(), dir.ToString())
                        + $"\0{flags & 0x2000}\0{show}\0{hotkey}\0{iconPath.ToString().ToUpperInvariant()}\0{iconIndex}";
                }
            }
            catch (Exception ex) { AppLogger.Error(ex, "Shortcut identity"); }
            finally { Release(instance); }
        }
        return Identity(item);
    }
    private static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
}
